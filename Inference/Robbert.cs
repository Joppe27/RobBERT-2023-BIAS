#region

using System.Numerics.Tensors;
using Microsoft.ML.OnnxRuntime;
using Tokenizers.DotNet;

#endregion

namespace RobBERT_2023_BIAS.Inference;

public class Robbert : IDisposable
{
    private InferenceSession _model = null!;
    private readonly RunOptions _runOptions = new();
    private Tokenizer _tokenizer = null!;

    private Robbert()
    {
    }

    public static async Task<Robbert> CreateAsync()
    {
        Robbert robbert = new();

        await robbert.InitializeAsync();

        return robbert;
    }

    private async Task InitializeAsync()
    {
        await Task.Run(() => { _model = new(Path.Combine(Environment.CurrentDirectory, "Resources/RobBERT-2023-large/model.onnx")); });
    }

    /// <param name="userInput">The sentence to be completed by the model. Must include a mask!</param>
    /// <param name="kCount">The amount of replacements for the mask the model should output.</param>
    /// <returns>A list containing a single dictionary for each mask with its possible replacements and probabilities, sorted by confidence.</returns>
    public async Task<List<Dictionary<string, float>>> Process(string userInput, int kCount)
    {
        // See tokenizer.json.  
        const int vocabSize = 50000;
        const int maskToken = 4;

        _tokenizer = new Tokenizer(Path.Combine(Environment.CurrentDirectory, "Resources/RobBERT-2023-large/tokenizer.json"));
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
        // We only care about the model's prediction for the <mask> token. For the other tokens, the model *should* always return the input token anyway.
        foreach (int mask in tokens.Index().Where(t => t.Item == maskToken).Select(i => i.Index * vocabSize))
            encodedMaskProbabilities.Add(encodedProbabilities.Slice(mask, vocabSize).ToArray());

        if (kCount >= 50)
            return await Task.Run(() => DecodeTokens(encodedMaskProbabilities, kCount));

        // When kCount is low, thread blocks for such a short time that running async is not worth the overhead.
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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _model.Dispose();
    }
}