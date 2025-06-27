// Copyright (c) Joppe27 <joppe27.be>. Licensed under the MIT Licence.
// See LICENSE file in repository root for full license text.

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
    private readonly Lock _progressLock = new();
    private readonly RunOptions _runOptions = new();

    private int _batchProgress;
    private InferenceSession _model = null!;
    private Tokenizer _tokenizer = null!;
    private int _tokenizerMask; // See tokenizer.json.
    private int _vocabSize; // See tokenizer.json.

    private LocalRobbert()
    {
    }

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

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        _model.Dispose();
    }

    public RobbertVersion Version { get; private set; }
    public event EventHandler<int>? BatchProgressChanged;

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

        uint[] tokensToDecode = [];
        if (wordToDecode == "<mask>")
        {
            tokensToDecode = [(uint)_tokenizerMask];
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
                    tokensToDecode = allTokens.Skip(i).Take(loopCount).ToArray();

                    // In some VERY rare cases (e.g. special chars), the tokenizer inserts spaces in the middle of the word. Therefore Replace instead of Trim.
                    comparison = _tokenizer.Decode(tokensToDecode).Replace(" ", "");

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

        List<float[]> probabilitiesPerToken = new();
        if (calculateProbability)
        {
            Span<float> encodedProbabilities = new float[logits.Length];
            TensorPrimitives.SoftMax(logits, encodedProbabilities);

            if (tokensToDecode.Length == 0)
            {
                foreach (int tokenStart in allTokens.Index().Select(i => i.Index * _vocabSize))
                    probabilitiesPerToken.Add(encodedProbabilities.Slice(tokenStart, _vocabSize).ToArray());
            }
            else
            {
                foreach (int maskStart in allTokens.Index().Where(t => tokensToDecode.Contains(t.Item)).Select(i => i.Index * _vocabSize))
                    probabilitiesPerToken.Add(encodedProbabilities.Slice(maskStart, _vocabSize).ToArray());
            }
        }
        else
        {
            if (tokensToDecode.Length == 0)
            {
                foreach (int tokenStart in allTokens.Index().Select(i => i.Index * _vocabSize))
                    probabilitiesPerToken.Add(logits.Slice(tokenStart, _vocabSize).ToArray());
            }
            else
            {
                foreach (int maskStart in allTokens.Index().Where(t => tokensToDecode.Contains(t.Item)).Select(i => i.Index * _vocabSize))
                    probabilitiesPerToken.Add(logits.Slice(maskStart, _vocabSize).ToArray());
            }
        }

        return await Task.Run(() => DecodeTokens(probabilitiesPerToken, kCount));
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

    private List<Dictionary<string, float>> DecodeTokens(List<float[]> probabilitiesPerToken, int kCount)
    {
        List<List<(int TokenId, float Logits)>> orderedProbabilitiesPerToken = new();
        List<Dictionary<string, float>> decodedProbabilitiesPerToken = new();

        foreach (float[] token in probabilitiesPerToken)
            orderedProbabilitiesPerToken.Add(token.Index().OrderByDescending(t => t.Item).ToList());

        foreach (var token in orderedProbabilitiesPerToken)
        {
            Dictionary<string, float> decodedCandidateTokens = new();

            for (int tokenProbabilityRank = 0; tokenProbabilityRank < kCount; tokenProbabilityRank++)
            {
                var tokenCandidate = token.ElementAt(tokenProbabilityRank);

                if (decodedCandidateTokens.TryAdd(_tokenizer.Decode([(uint)tokenCandidate.TokenId]).Trim(), tokenCandidate.Logits) == false)
                {
                    // The service provider can be null here because LocalRobbert also gets used server-side (which is completely separate from App)
                    if (App.ServiceProvider != null)
                    {
                        var logger = App.ServiceProvider.GetRequiredService<ILogSink>();

                        // Ignored duplicates caused by leading/trailing spaces which get trimmed during decode. In this case, the highest logits number out of all possibilities is returned.
                        logger.Log(LogEventLevel.Warning, "NON-AVALONIA", this, "Token ignored during decoding of masks");
                    }
                }
            }

            decodedProbabilitiesPerToken.Add(decodedCandidateTokens);
        }

        return decodedProbabilitiesPerToken;
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