using Microsoft.ML.OnnxRuntime;
using Tokenizers.DotNet;

namespace RobBERT_2023_BIAS.Inference;

public class Robbert : IDisposable
{
    private readonly InferenceSession _model = new(Path.Combine(Environment.CurrentDirectory, "Resources/RobBERT-2023-large/model.onnx"));
    private readonly RunOptions _runOptions = new();
    
    /// <param name="userInput">The sentence to be completed by the model. Must include a mask!</param>
    /// <param name="kCount">The amount of replacements for the mask the model should output.</param>
    /// <returns>A list of replacements for the mask, sorted by confidence.</returns>
    public Dictionary<string, float> Prompt(string userInput, int kCount)
    {
        // The size of the RobBERT-2023-large vocabulary is 50000, see tokenizer.json.  
        const int vocabSize = 50000;
        Dictionary<string, float> answer = new();
        
        var tokenizer = new Tokenizer(Path.Combine(Environment.CurrentDirectory, "Resources/RobBERT-2023-large/tokenizer.json"));
        var tokens = tokenizer.Encode(userInput);
        
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

        using var output = _model.Run(_runOptions, inputs, _model.OutputNames);

        var logits = output.First().GetTensorDataAsSpan<float>();

        var maskLogits = logits.Slice(Array.IndexOf(tokens, (uint)4) * vocabSize, vocabSize).ToArray();
        var orderedMaskLogits = maskLogits.OrderDescending().ToArray();

        for (var i = 0; i < kCount; i++)
            answer.TryAdd(tokenizer.Decode([(uint)Array.IndexOf(maskLogits, orderedMaskLogits[i])]).Trim(), orderedMaskLogits[i]);

        return answer;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _model.Dispose();
    }
}