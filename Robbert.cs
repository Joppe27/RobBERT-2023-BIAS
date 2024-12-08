using Microsoft.ML.OnnxRuntime;
using Tokenizers.DotNet;

namespace RobBERT_2023_BIAS;

public class Robbert
{
    public void Prompt(string input, int kCount)
    {
        // The size of the RobBERT-2023-large vocabulary is 50000, see tokenizer.json.  
        const int vocabSize = 50000;
        
        var tokenizer = new Tokenizer(Path.Combine(Environment.CurrentDirectory, "Resources/RobBERT-2023-large/tokenizer.json"));
        var tokens = tokenizer.Encode(input);
        
        var robbertInput = new RobbertInput()
        {
            InputIds = Array.ConvertAll(tokens, token => (long)token),
            AttentionMask = Enumerable.Repeat((long)1, tokens.Length).ToArray() // All tokens given same attention for now.
        };
        
        var model = new InferenceSession(Path.Combine(Environment.CurrentDirectory, "Resources/RobBERT-2023-large/model.onnx")); // TODO: a new session does not need to be started every prompt obviously
        var runOptions = new RunOptions();
        
        using var inputOrt = OrtValue.CreateTensorValueFromMemory(robbertInput.InputIds, new long[] { 1, robbertInput.InputIds.Length });
        using var attOrt = OrtValue.CreateTensorValueFromMemory(robbertInput.AttentionMask, new long[] { 1, robbertInput.AttentionMask.Length });

        var inputs = new Dictionary<string, OrtValue>()
        {
            { "input_ids", inputOrt },
            { "attention_mask", attOrt },
        };

        using var output = model.Run(runOptions, inputs, model.OutputNames);

        var logits = output.First().GetTensorDataAsSpan<float>();

        var maskLogits = logits.Slice(Array.IndexOf(tokens, (uint)4) * vocabSize, vocabSize).ToArray();
        var orderedMaskLogits = maskLogits.OrderDescending().ToArray();

        uint[] topK = new uint[kCount];
        for (var i = 0; i < kCount; i++)
        {
            topK[i] = (uint)Array.IndexOf(maskLogits, orderedMaskLogits[i]);
        }

        var predictedToken = tokenizer.Decode(topK);
        
        Console.WriteLine(predictedToken);
    }
}