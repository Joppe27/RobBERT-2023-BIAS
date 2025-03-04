#region

using System.Numerics.Tensors;
using Microsoft.ML.OnnxRuntime;
using Tokenizers.DotNet;

#endregion

namespace RobBERT_2023_BIAS.Inference;

public class Robbert : IDisposable
{
    public enum RobbertVersion
    {
        Base2022,
        Base2023,
        Large2023,
    }

    private readonly RunOptions _runOptions = new();
    private InferenceSession _model = null!;
    private RobbertVersion _robbertVersion;
    private Tokenizer _tokenizer = null!;
    private int _vocabSize; // See tokenizer.json.

    private Robbert()
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _model.Dispose();
    }

    public static async Task<Robbert> CreateAsync(RobbertVersion version)
    {
        Robbert robbert = new();
        robbert._robbertVersion = version;

        await robbert.InitializeAsync();

        return robbert;
    }

    private async Task InitializeAsync()
    {
        string modelPath;
        string tokenizerPath;

        switch (_robbertVersion)
        {
            case RobbertVersion.Base2022:
                modelPath = "Resources/RobBERT-2022-base/model.onnx";
                tokenizerPath = "Resources/RobBERT-2022-base/tokenizer.json";
                _vocabSize = 42774;
                break;

            case RobbertVersion.Base2023:
                modelPath = "Resources/RobBERT-2023-base/model.onnx";
                tokenizerPath = "Resources/RobBERT-2023-base/tokenizer.json";
                _vocabSize = 50000;
                break;

            case RobbertVersion.Large2023:
                modelPath = "Resources/RobBERT-2023-large/model.onnx";
                tokenizerPath = "Resources/RobBERT-2023-large/tokenizer.json";
                _vocabSize = 50000;
                break;

            default:
                throw new InvalidOperationException("Unsupported RobBERT version requested");
        }

        await Task.Run(() =>
        {
            _model = new(Path.Combine(Environment.CurrentDirectory, modelPath));
            _tokenizer = new Tokenizer(Path.Combine(Environment.CurrentDirectory, tokenizerPath));
        });
    }

    /// <param name="userInput">The sentence to be completed by the model. Must include a mask!</param>
    /// <param name="kCount">The amount of replacements for the mask the model should output.</param>
    /// <param name="maskToken">Which specific token to decode. If null, all tokens are decoded.</param>
    /// <returns>A list containing a single dictionary for each mask with its possible replacements and probabilities, sorted by confidence.</returns>
    public async Task<List<Dictionary<string, float>>> Process(string userInput, int kCount, string? maskToken = "<mask>", bool calculateProbability = true)
    {
        // TODO: fix this garbage
        // TODO: robbert2022 broken
        var tokens = _tokenizer.Encode(userInput);
        int mask = -1;

        if (maskToken != null)
        {
            foreach (uint token in tokens)
            {
                if (_tokenizer.Decode([token]).Trim() == maskToken)
                {
                    mask = (int)token;
                    break;
                }
            }
        }

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

            if (mask < 0)
            {
                foreach (int tokenStart in tokens.Index().Select(i => i.Index * _vocabSize))
                    encodedMaskProbabilities.Add(encodedProbabilities.Slice(tokenStart, _vocabSize).ToArray());
            }
            else
            {
                foreach (int maskStart in tokens.Index().Where(t => t.Item == mask).Select(i => i.Index * _vocabSize))
                    encodedMaskProbabilities.Add(encodedProbabilities.Slice(maskStart, _vocabSize).ToArray());
            }
        }
        else
        {
            if (mask < 0)
            {
                foreach (int tokenStart in tokens.Index().Select(i => i.Index * _vocabSize))
                    encodedMaskProbabilities.Add(logits.Slice(tokenStart, _vocabSize).ToArray());
            }
            else
            {
                foreach (int maskStart in tokens.Index().Where(t => t.Item == mask).Select(i => i.Index * _vocabSize))
                    encodedMaskProbabilities.Add(logits.Slice(maskStart, _vocabSize).ToArray());
            }
        }

        return await Task.Run(() => DecodeTokens(encodedMaskProbabilities, kCount));
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
}