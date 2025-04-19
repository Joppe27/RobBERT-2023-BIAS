namespace RobBERT_2023_BIAS.Inference;

public interface IRobbert
{
    RobbertVersion Version { get; }
    
    event EventHandler<int> BatchProgressChanged;
    
    Task<List<Dictionary<string, float>>> Process(string userInput, int kCount, string? maskToken = "<mask>", bool calculateProbability = true);

    Task<List<List<Dictionary<string, float>>>> ProcessBatch(List<RobbertPrompt> userInput, int kCount, bool calculateProbability = true);

    ValueTask DisposeAsync();
}