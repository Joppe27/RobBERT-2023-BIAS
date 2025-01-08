using ExCSS;
using Microsoft.ML.OnnxRuntime;
using Tokenizers.DotNet;

namespace RobBERT_2023_BIAS.Inference;

public class Robbert : IDisposable
{
    private InferenceSession _model = null!;
    private readonly RunOptions _runOptions = new();

    private Robbert() {}
    
    public static async Task<Robbert> CreateAsync()
    {
        Robbert robbert = new();

        await robbert.InitializeAsync();

        return robbert;
    }

    private async Task InitializeAsync()
    {
        await Task.Run(() =>
        {
            _model = new(Path.Combine(Environment.CurrentDirectory, "Resources/RobBERT-2023-large/model.onnx"));
        });
    }
    
    /// <param name="userInput">The sentence to be completed by the model. Must include a mask!</param>
    /// <param name="kCount">The amount of replacements for the mask the model should output.</param>
    /// <returns>A list of replacements for the mask, sorted by confidence.</returns>
    public async Task<Dictionary<string, float>> Prompt(string userInput, int kCount)
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

        using var output = await Task.Run(() => _model.Run(_runOptions, inputs, _model.OutputNames));
        
        // Memory instead of span due to C# 12 limitation, see https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-13.0/ref-unsafe-in-iterators-async
        ReadOnlyMemory<float> logits = new Memory<float>(output[0].GetTensorDataAsSpan<float>().ToArray());
        
        var maskLogits = logits.Slice(Array.IndexOf(tokens, (uint)4) * vocabSize, vocabSize).ToArray();
        var orderedMaskLogits = maskLogits.OrderDescending().ToArray();
        
        // When kCount is low, thread blocks for such a short time that running async isn't necessary because of overhead creating/switching thread
        if (kCount > 50)
        {
            await Task.Run(() =>
            {
                for (var i = 0; i < kCount; i++)
                    answer.TryAdd(tokenizer.Decode([(uint)Array.IndexOf(maskLogits, orderedMaskLogits[i])]).Trim(), orderedMaskLogits[i]);
            });
        }
        else
        {
            for (var i = 0; i < kCount; i++)
                answer.TryAdd(tokenizer.Decode([(uint)Array.IndexOf(maskLogits, orderedMaskLogits[i])]).Trim(), orderedMaskLogits[i]);
        }
        

        return answer;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _model.Dispose();
    }
}