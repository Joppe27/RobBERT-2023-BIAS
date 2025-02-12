#region

using System.Numerics.Tensors;
using Microsoft.ML.OnnxRuntime;
using Tokenizers.DotNet;

#endregion

namespace RobBERT_2023_BIAS.Inference;

public class Robbert : IDisposable
{
    private RobbertVersion _robbertVersion; 
    
    private InferenceSession _model = null!;
    private readonly RunOptions _runOptions = new();
    private Tokenizer _tokenizer = null!;
    private int _vocabSize; // See tokenizer.json.
    private int _maskToken; // See tokenizer_config.json.

    private Robbert()
    {
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
                _maskToken = 39984;
                break;

            case RobbertVersion.Base2023:
                modelPath = "Resources/RobBERT-2023-base/model.onnx";
                tokenizerPath = "Resources/RobBERT-2023-base/tokenizer.json";
                _vocabSize = 50000;
                _maskToken = 4;
                break;

            case RobbertVersion.Large2023:
                modelPath = "Resources/RobBERT-2023-large/model.onnx";
                tokenizerPath = "Resources/RobBERT-2023-large/tokenizer.json";
                _vocabSize = 50000;
                _maskToken = 4;
                break;

            default:
                throw new Exception(); // TODO: add more info about all exceptions everywhere
        }

        await Task.Run(() =>
        {
            _model = new(Path.Combine(Environment.CurrentDirectory, modelPath));
            _tokenizer = new Tokenizer(Path.Combine(Environment.CurrentDirectory, tokenizerPath));
        });
    }

    /// <param name="userInput">The sentence to be completed by the model. Must include a mask!</param>
    /// <param name="kCount">The amount of replacements for the mask the model should output.</param>
    /// <param name="decodeAll">Whether to decode only the mask token or all input tokens.</param>
    /// <returns>A list containing a single dictionary for each mask with its possible replacements and probabilities, sorted by confidence.</returns>
    public async Task<List<Dictionary<string, float>>> Process(string userInput, int kCount, bool decodeAll = false)
    {
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
        Span<float> encodedProbabilities = new float[logits.Length];
        TensorPrimitives.SoftMax(logits, encodedProbabilities);

        List<float[]> encodedMaskProbabilities = new();

        if (decodeAll)
        {
            // The first 185 tokens are special tokens or punctuation marks and their decoding is therefore not relevant. See tokenizer.json.
            // This does not impact probability as it is calculated before these tokens are dropped. TODO: REINVESTIGATE
            foreach (int tokenStart in tokens.Index().Select(i => i.Index * _vocabSize))
                encodedMaskProbabilities.Add(encodedProbabilities.Slice(tokenStart, _vocabSize).ToArray());
        }
        else
        {
            foreach (int maskStart in tokens.Index().Where(t => t.Item == _maskToken).Select(i => i.Index * _vocabSize))
                encodedMaskProbabilities.Add(encodedProbabilities.Slice(maskStart, _vocabSize).ToArray());
        }

        if (kCount >= 50 || decodeAll)
            return await Task.Run(() => DecodeTokens(encodedMaskProbabilities, kCount));

        // When token count is low, thread blocks for such a short time that running async is not worth the overhead.
        return DecodeTokens(encodedMaskProbabilities, kCount);
    }

    private List<Dictionary<string, float>> DecodeTokens(List<float[]> encodedMaskProbabilities, int kCount)
    {
        List<Dictionary<string, float>> decodedMaskProbabilities = new();
        List<float[]> sortedEncodedMaskProbabilities = encodedMaskProbabilities.Select(m => (float[])m.Clone()).ToList();

        foreach (float[] encodedCandidateTokens in sortedEncodedMaskProbabilities)
        {
            Array.Sort(encodedCandidateTokens);
            Array.Reverse(encodedCandidateTokens);
        }

        for (int mask = 0; mask < encodedMaskProbabilities.Count; mask++)
        {
            Dictionary<string, float> decodedCandidateTokens = new();

            for (var i = 0; i < kCount; i++)
                if (decodedCandidateTokens.TryAdd(_tokenizer.Decode([(uint)Array.IndexOf(encodedMaskProbabilities[mask], sortedEncodedMaskProbabilities[mask][i])]).Trim(), sortedEncodedMaskProbabilities[mask][i]) == false)
                    Console.WriteLine("IGNORED TOKEN!"); // Ignored duplicates probably happen because of leading/trailing spaces which get trimmed during decode (see line above).

            decodedMaskProbabilities.Add(decodedCandidateTokens);
        }

        return decodedMaskProbabilities;
    }

    public enum RobbertVersion
    {
        Base2022,
        Base2023,
        Large2023,
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _model.Dispose();
    }
}