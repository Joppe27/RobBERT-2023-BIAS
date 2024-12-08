using Microsoft.ML.OnnxRuntime;
using Tokenizers.DotNet;

namespace RobBERT_2023_BIAS;

public class Prompter
{
    public Prompter()
    {
        Run();
    }

    private void Run()
    {
        const int vocab_size = 50000;
        
        var tokenizer = new Tokenizer(Path.Combine(Environment.CurrentDirectory, "Resources/RobBERT-2023-large/tokenizer.json"));
        var tokens = tokenizer.Encode("De hoofdstad van België is <mask>.");
        
        var robbertInput = new RobbertInput()
        {
            InputIds = Array.ConvertAll(tokens, token => (long)token),
            AttentionMask = Array.ConvertAll(tokens, token => (long)token),
        };
        
        var model = new InferenceSession(Path.Combine(Environment.CurrentDirectory, "Resources/RobBERT-2023-large/model.onnx"));
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

        var maskLogits = logits.Slice(Array.IndexOf(tokens, (uint)4) * vocab_size, vocab_size).ToArray();

        var predictedToken = tokenizer.Decode([(uint)Array.IndexOf(maskLogits, maskLogits.Max())]);
        
        Console.WriteLine(predictedToken);
    }
    
    private int GetMaxValueIndex(ReadOnlySpan<float> span)
    {
        float maxVal = span[0];
        int maxIndex = 0;
        for (int i = 1; i < span.Length; i++)
        {
            var v = span[i];
            if (v > maxVal)
            {
                maxVal = v;
                maxIndex = i;
            }
        }

        return maxIndex;
    }
}