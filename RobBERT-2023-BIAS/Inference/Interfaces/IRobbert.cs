namespace RobBERT_2023_BIAS.Inference;

public interface IRobbert
{
    Task<List<Dictionary<string, float>>> Process(string userInput, int kCount, string? maskToken = "<mask>", bool calculateProbability = true);

    void Dispose();
}