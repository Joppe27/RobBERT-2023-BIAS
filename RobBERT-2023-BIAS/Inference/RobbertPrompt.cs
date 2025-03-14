namespace RobBERT_2023_BIAS.Inference;

public class RobbertPrompt(string sentence, string mask)
{
    public string Sentence { get; set; } = sentence;

    public string Mask { get; set; } = mask;
}