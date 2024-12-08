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
            AttentionMask = Enumerable.Repeat((long)1, tokens.Length).ToArray(),
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
        var orderedMaskLogits = maskLogits.OrderDescending().ToArray();

        const int kCount = 5;
        uint[] topK = new uint[kCount];
        for (var i = 0; i < kCount; i++)
        {
            topK[i] = (uint)Array.IndexOf(maskLogits, orderedMaskLogits[i]);
        }

        var predictedToken = tokenizer.Decode(topK);
        
        Console.WriteLine(predictedToken);
    }
}