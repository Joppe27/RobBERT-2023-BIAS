#region

using System.Net.Http.Json;
using System.Numerics.Tensors;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Microsoft.ML.OnnxRuntime;
using Tokenizers.DotNet;

#endregion

namespace RobBERT_2023_BIAS.Inference;

public class LocalRobbert : IDisposable, IRobbert
{
    private readonly RunOptions _runOptions = new();
    private InferenceSession _model = null!;
    private Tokenizer _tokenizer = null!;
    private int _vocabSize; // See tokenizer.json.
    private int _tokenizerMask; // See tokenizer.json.

    public RobbertVersion Version { get; private set; }
    public event EventHandler<int>? BatchProgressChanged;

    private int _batchProgress;

    private int BatchProgress
    {
        get => _batchProgress;
        set
        {
            if (BatchProgressChanged != null && (value > BatchProgress || value == 0))
                BatchProgressChanged.Invoke(this, value);

            _batchProgress = value;
        }
    }

    private LocalRobbert()
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _model.Dispose();
    }

    /// <param name="userInput">The sentence to be completed by the model. Must include a mask!</param>
    /// <param name="kCount">The amount of replacements for the mask the model should output.</param>
    /// <param name="maskToken">Which specific token to decode. If null, all tokens are decoded.</param>
    /// <param name="calculateProbability">Whether to calculate the token candidates' probability. If false, logits are returned.</param>
    /// <returns>A list containing a single dictionary for each mask with its possible replacements and probabilities, sorted by confidence.</returns>
    public async Task<List<Dictionary<string, float>>> Process(string userInput, int kCount, string? maskToken = "<mask>", bool calculateProbability = true)
    {
        if (maskToken != null)
        {
            var maskRegex = new Regex(@$"(?<!\w){maskToken}(?!\w)");
            userInput = maskRegex.Replace(userInput, "<mask>");
        }
        
        var tokens = _tokenizer.Encode(userInput);
        
        var robbertInput = new RobbertInput()
        {
            InputIds = Array.ConvertAll(tokens, token => (long)token),
            AttentionMask = Enumerable.Repeat((long)1, tokens.Length).ToArray() // All tokens given same attention for now.
        };

        using var inputOrt = OrtValue.CreateTensorValueFromMemory(robbertInput.InputIds, new long[] { 1, robbertInput.InputIds.Length });
        using var attOrt = OrtValue.CreateTensorValueFromMemory(robbertInput.AttentionMask, new long[] { 1, robbertInput.AttentionMask.Length });

        var inputs = new Dictionary<string, OrtValue>()
        {
            { "input_ids", inputOrt },
            { "attention_mask", attOrt },
        };

        using var output = await Task.Run(() => _model.Run(_runOptions, inputs, _model.OutputNames));

        ReadOnlySpan<float> logits = output[0].GetTensorDataAsSpan<float>();

        List<float[]> encodedMaskProbabilities = new();
        if (calculateProbability)
        {
            Span<float> encodedProbabilities = new float[logits.Length];
            TensorPrimitives.SoftMax(logits, encodedProbabilities);

            if (maskToken == null)
            {
                foreach (int tokenStart in tokens.Index().Select(i => i.Index * _vocabSize))
                    encodedMaskProbabilities.Add(encodedProbabilities.Slice(tokenStart, _vocabSize).ToArray());
            }
            else
            {
                foreach (int maskStart in tokens.Index().Where(t => t.Item == _tokenizerMask).Select(i => i.Index * _vocabSize))
                    encodedMaskProbabilities.Add(encodedProbabilities.Slice(maskStart, _vocabSize).ToArray());
            }
        }
        else
        {
            if (maskToken == null)
            {
                foreach (int tokenStart in tokens.Index().Select(i => i.Index * _vocabSize))
                    encodedMaskProbabilities.Add(logits.Slice(tokenStart, _vocabSize).ToArray());
            }
            else
            {
                foreach (int maskStart in tokens.Index().Where(t => t.Item == _tokenizerMask).Select(i => i.Index * _vocabSize))
                    encodedMaskProbabilities.Add(logits.Slice(maskStart, _vocabSize).ToArray());
            }
        }

        return await Task.Run(() => DecodeTokens(encodedMaskProbabilities, kCount));
    }

    public async Task<List<List<Dictionary<string, float>>>> ProcessBatch(List<RobbertPrompt> userInput, int kCount,
        bool calculateProbability = true)
    {
        List<Dictionary<string, float>>[] modelOutputs = new List<Dictionary<string, float>>[userInput.Count];

        BatchProgress = 0;
        
        await Parallel.ForAsync(0, userInput.Count, async (i, _) =>
        {
            // Important: sentences are returned in the original order here as analysis depends on this!
            modelOutputs[i] = await Process(userInput[i].Sentence, kCount, userInput[i].Mask, calculateProbability);
            BatchProgress = (int)((float)i / userInput.Count * 100);
        });

        return modelOutputs.ToList();
    }

    private List<Dictionary<string, float>> DecodeTokens(List<float[]> encodedMaskProbabilities, int kCount)
    {
        List<Dictionary<string, float>> decodedMaskProbabilities = new();
        List<float[]> sortedEncodedMaskProbabilities = encodedMaskProbabilities.Select(m => (float[])m.Clone()).ToList();

        // Sort by from highest to lowest logits to be able to sample via top-k (TODO: expensive, optimization somehow?)
        foreach (float[] encodedCandidateTokens in sortedEncodedMaskProbabilities)
        {
            Array.Sort(encodedCandidateTokens);
            Array.Reverse(encodedCandidateTokens);
        }

        for (int mask = 0; mask < encodedMaskProbabilities.Count; mask++)
        {
            Dictionary<string, float> decodedCandidateTokens = new();
            for (int candidate = 0; candidate < kCount; candidate++)
            {
                if (decodedCandidateTokens.TryAdd(_tokenizer.Decode([
                        (uint)Array.IndexOf(encodedMaskProbabilities[mask],
                            sortedEncodedMaskProbabilities[mask][candidate])
                    ]).Trim(), sortedEncodedMaskProbabilities[mask][candidate]) == false)
                {
                    // Ignored duplicates probably happen because of leading/trailing spaces which get trimmed during decode (see line above).
                    Console.WriteLine("IGNORED TOKEN!");
                }
            }

            decodedMaskProbabilities.Add(decodedCandidateTokens);
        }

        return decodedMaskProbabilities;
    }

    public class Factory : IRobbertFactory
    {
        public async Task<IRobbert> Create(RobbertVersion version)
        {
            LocalRobbert localRobbert = new();

            string modelPath;
            string tokenizerPath;

            switch (version)
            {
                case RobbertVersion.Base2022:
                    modelPath = "Resources/RobBERT-2022-base/model.onnx";
                    tokenizerPath = "Resources/RobBERT-2022-base/tokenizer.json";
                    localRobbert._vocabSize = 42774;
                    localRobbert._tokenizerMask = 39984;
                    break;

                case RobbertVersion.Base2023:
                    modelPath = "Resources/RobBERT-2023-base/model.onnx";
                    tokenizerPath = "Resources/RobBERT-2023-base/tokenizer.json";
                    localRobbert._vocabSize = 50000;
                    localRobbert._tokenizerMask = 4;
                    break;

                case RobbertVersion.Large2023:
                    modelPath = "Resources/RobBERT-2023-large/model.onnx";
                    tokenizerPath = "Resources/RobBERT-2023-large/tokenizer.json";
                    localRobbert._vocabSize = 50000;
                    localRobbert._tokenizerMask = 4;
                    break;

                default:
                    throw new InvalidOperationException("Unsupported RobBERT version requested");
            }

            localRobbert.Version = version;

            await Task.Run(() =>
            {
                localRobbert._model = new InferenceSession(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelPath));
                localRobbert._tokenizer = new Tokenizer(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, tokenizerPath));
            });

            return localRobbert;
        }

        public async Task<IRobbert> CreateFromBlob(RobbertVersion version)
        {
            LocalRobbert localRobbert = new();

            switch (version)
            {
                case RobbertVersion.Base2022:
                    localRobbert._vocabSize = 42774;
                    localRobbert._tokenizerMask = 39984;
                    break;

                case RobbertVersion.Base2023:
                    localRobbert._vocabSize = 50000;
                    localRobbert._tokenizerMask = 4;
                    break;

                case RobbertVersion.Large2023:
                    localRobbert._vocabSize = 50000;
                    localRobbert._tokenizerMask = 4;
                    break;

                default:
                    throw new InvalidOperationException("Unsupported RobBERT version requested");
            }

            var httpClient = new HttpClient() { BaseAddress = new Uri("http://localhost:5164/api/") };

            var httpResponse = await httpClient.GetAsync($"getrobbertsas?version={(int)version}");

            var containerUri = await httpResponse.Content.ReadFromJsonAsync<string>() ?? throw new NullReferenceException();

            var containerClient = new BlobContainerClient(new Uri(containerUri));

            var modelClient = containerClient.GetBlobClient("model.onnx");
            var modelStream = new MemoryStream();
            await modelClient.DownloadToAsync(modelStream);

            var tokenizerClient = containerClient.GetBlobClient("tokenizer.json");
            var tokenizerStream = File.Create("tokenizer.json");
            await tokenizerClient.DownloadToAsync(tokenizerStream);

            await Task.Run(() =>
            {
                localRobbert._model = new InferenceSession(modelStream.ToArray());
                modelStream.Dispose();

                localRobbert._tokenizer = new Tokenizer("tokenizer.json");
            });

            localRobbert.Version = version;

            return localRobbert;
        }
    }
}