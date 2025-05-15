#region

using System.Numerics.Tensors;
using System.Text.RegularExpressions;
using Avalonia.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML.OnnxRuntime;
using Tokenizers.DotNet;

#endregion

namespace RobBERT_2023_BIAS.Inference;

public class LocalRobbert : IAsyncDisposable, IRobbert
{
    private readonly RunOptions _runOptions = new();
    private InferenceSession _model = null!;
    private Tokenizer _tokenizer = null!;
    private int _vocabSize; // See tokenizer.json.
    private int _tokenizerMask; // See tokenizer.json.

    public RobbertVersion Version { get; private set; }
    public event EventHandler<int>? BatchProgressChanged;

    private int _batchProgress;
    private readonly Lock _progressLock = new();

    private int BatchProgress
    {
        get => _batchProgress;
        set
        {
            bool invokeHandler = false;

            lock (_progressLock)
            {
                if (value > _batchProgress || value == 0)
                {
                    _batchProgress = value;
                    invokeHandler = true;
                }
            }

            if (invokeHandler && BatchProgressChanged != null)
                BatchProgressChanged.Invoke(this, value);
        }
    }

    private LocalRobbert()
    {
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        _model.Dispose();
    }

    /// <param name="userInput">The sentence to be completed by the model. Must include a mask!</param>
    /// <param name="kCount">The amount of replacements for the mask the model should output.</param>
    /// <param name="wordToMask">Which specific word to mask (all occurences). If null, no tokens are masked.</param>
    /// <param name="wordToDecode">Which specific word to decode (all occurences). If null, all tokens are decoded.</param>
    /// <param name="calculateProbability">Whether to calculate the token candidates' probability. If false, logits are returned.</param>
    /// <returns>A list containing a single dictionary for each mask with its possible replacements and probabilities, sorted by confidence.</returns>
    public async Task<List<Dictionary<string, float>>> Process(string userInput, int kCount, string? wordToMask = "<mask>", string? wordToDecode = "<mask>",
        bool calculateProbability = true)
    {
        if (wordToMask != null)
        {
            var maskRegex = new Regex(@$"(?<!\w){wordToMask}(?!\w)");
            userInput = maskRegex.Replace(userInput, "<mask>");
        }

        var allTokens = _tokenizer.Encode(userInput);

        uint[] specificTokensToDecode = [];
        if (wordToDecode == "<mask>")
        {
            specificTokensToDecode = [(uint)_tokenizerMask];
        }
        else if (wordToDecode != null)
        {
            string comparison = "";
            int loopCount = 1;
            while (comparison != wordToDecode)
            {
                if (loopCount > allTokens.Length)
                    throw new InvalidOperationException();

                for (int i = 0; i < allTokens.Length; i++)
                {
                    specificTokensToDecode = allTokens.Skip(i).Take(loopCount).ToArray();

                    // In some VERY rare cases (e.g. special chars), the tokenizer inserts spaces in the middle of the word. Therefore Replace instead of Trim.
                    comparison = _tokenizer.Decode(specificTokensToDecode).Replace(" ", "");

                    if (comparison == wordToDecode)
                        break;
                }

                loopCount++;
            }
        }
        
        var robbertInput = new RobbertInput()
        {
            InputIds = Array.ConvertAll(allTokens, token => (long)token),
            AttentionMask = Enumerable.Repeat((long)1, allTokens.Length).ToArray() // All tokens given same attention for now.
        };

        ReadOnlySpan<float> logits;

        using (OrtValue inputOrt = OrtValue.CreateTensorValueFromMemory(robbertInput.InputIds, new long[] { 1, robbertInput.InputIds.Length }),
               attOrt = OrtValue.CreateTensorValueFromMemory(robbertInput.AttentionMask, new long[] { 1, robbertInput.AttentionMask.Length }))
        {
            List<string> inputNames = new() { "input_ids", "attention_mask" };
            List<OrtValue> inputValues = new() { inputOrt, attOrt };

            var output = _model.Run(_runOptions, inputNames, inputValues, _model.OutputNames);

            logits = output[0].GetTensorDataAsSpan<float>();
        }

        List<float[]> encodedMaskProbabilities = new();
        if (calculateProbability)
        {
            Span<float> encodedProbabilities = new float[logits.Length];
            TensorPrimitives.SoftMax(logits, encodedProbabilities);

            if (specificTokensToDecode.Length == 0)
            {
                foreach (int tokenStart in allTokens.Index().Select(i => i.Index * _vocabSize))
                    encodedMaskProbabilities.Add(encodedProbabilities.Slice(tokenStart, _vocabSize).ToArray());
            }
            else
            {
                foreach (int maskStart in allTokens.Index().Where(t => specificTokensToDecode.Contains(t.Item)).Select(i => i.Index * _vocabSize))
                    encodedMaskProbabilities.Add(encodedProbabilities.Slice(maskStart, _vocabSize).ToArray());
            }
        }
        else
        {
            if (specificTokensToDecode.Length == 0)
            {
                foreach (int tokenStart in allTokens.Index().Select(i => i.Index * _vocabSize))
                    encodedMaskProbabilities.Add(logits.Slice(tokenStart, _vocabSize).ToArray());
            }
            else
            {
                foreach (int maskStart in allTokens.Index().Where(t => specificTokensToDecode.Contains(t.Item)).Select(i => i.Index * _vocabSize))
                    encodedMaskProbabilities.Add(logits.Slice(maskStart, _vocabSize).ToArray());
            }
        }

        return await Task.Run(() => DecodeTokens(encodedMaskProbabilities, kCount));
    }

    public async Task<List<List<Dictionary<string, float>>>> ProcessBatch(List<RobbertPrompt> userInput, int kCount,
        CancellationToken token, bool calculateProbability = true)
    {
        List<Dictionary<string, float>>[] modelOutputs = new List<Dictionary<string, float>>[userInput.Count];

        BatchProgress = 0;

        await Parallel.ForAsync(0, userInput.Count,
            new ParallelOptions() { CancellationToken = token, MaxDegreeOfParallelism = Environment.ProcessorCount - 1 }, async (i, ct) =>
        {
            if (ct.IsCancellationRequested)
                return;
                        
            // Important: sentences are returned in the original order here as analysis depends on this!
            modelOutputs[i] = await Process(userInput[i].Sentence, kCount, null, userInput[i].WordToDecode, calculateProbability);
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
                    // The service provider can be null here because LocalRobbert also gets used server-side (which is completely separate from App)
                    if (App.ServiceProvider != null)
                    {
                        var logger = App.ServiceProvider.GetRequiredService<ILogSink>();

                        // Ignored duplicates caused by leading/trailing spaces which get trimmed during decode (see line above). In this case, the highest logits number out of all possibilities is returned.
                        logger.Log(LogEventLevel.Warning, "NON-AVALONIA", this, "Token ignored during decoding of masks");
                    }
                }
            }

            decodedMaskProbabilities.Add(decodedCandidateTokens);
        }

        return decodedMaskProbabilities;
    }

    public class Factory : IRobbertFactory
    {
        public async Task<IRobbert> Create(RobbertVersion version, bool usingBlobs = false)
        {
            LocalRobbert localRobbert = new();
            
            string modelPath;
            string tokenizerPath;

            switch (version)
            {
                case RobbertVersion.Base2022:
                    modelPath = $"{(usingBlobs ? "BlobResources" : "Resources")}/RobBERT-2022-base/model.onnx";
                    tokenizerPath = $"{(usingBlobs ? "BlobResources" : "Resources")}/RobBERT-2022-base/tokenizer.json";
                    localRobbert._vocabSize = 42774;
                    localRobbert._tokenizerMask = 39984;
                    break;

                case RobbertVersion.Base2023:
                    modelPath = $"{(usingBlobs ? "BlobResources" : "Resources")}/RobBERT-2023-base/model.onnx";
                    tokenizerPath = $"{(usingBlobs ? "BlobResources" : "Resources")}/RobBERT-2023-base/tokenizer.json";
                    localRobbert._vocabSize = 50000;
                    localRobbert._tokenizerMask = 4;
                    break;

                case RobbertVersion.Large2023:
                    modelPath = $"{(usingBlobs ? "BlobResources" : "Resources")}/RobBERT-2023-large/model.onnx";
                    tokenizerPath = $"{(usingBlobs ? "BlobResources" : "Resources")}/RobBERT-2023-large/tokenizer.json";
                    localRobbert._vocabSize = 50000;
                    localRobbert._tokenizerMask = 4;
                    break;

                default:
                    throw new InvalidOperationException("Unsupported RobBERT version requested");
            }

            await Task.Run(() =>
            {
                localRobbert._model = new InferenceSession(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelPath),
                    new SessionOptions() { EnableCpuMemArena = false });
                localRobbert._tokenizer = new Tokenizer(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, tokenizerPath));
            });
            
            localRobbert.Version = version;

            return localRobbert;
        }
    }
}