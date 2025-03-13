namespace RobBERT_2023_BIAS.Inference;

public interface IRobbert
{
    event EventHandler<int> BatchProgressChanged;
    
    Task<List<Dictionary<string, float>>> Process(string userInput, int kCount, string? maskToken = "<mask>", bool calculateProbability = true);

    Task<List<List<Dictionary<string, float>>>> ProcessBatch(List<(string Sentence, string Mask)> userInput, int kCount, bool calculateProbability = true);

    void Dispose();
}