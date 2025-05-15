namespace RobBERT_2023_BIAS.Inference;

public interface IRobbert
{
    RobbertVersion Version { get; }
    
    event EventHandler<int> BatchProgressChanged;

    Task<List<Dictionary<string, float>>> Process(string userInput, int kCount, string? wordToMask = "<mask>", string? wordToDecode = "<mask>",
        bool calculateProbability = true);

    Task<List<List<Dictionary<string, float>>>> ProcessBatch(List<RobbertPrompt> userInput, int kCount, CancellationToken token,
        bool calculateProbability = true);

    ValueTask DisposeAsync();
}