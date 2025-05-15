namespace RobBERT_2023_BIAS.Inference;

public class RobbertPrompt(string sentence, string? wordToMask = null, string? wordToDecode = null)
{
    public string Sentence { get; set; } = sentence;

    public string? WordToMask { get; set; } = wordToMask;

    public string? WordToDecode { get; set; } = wordToDecode;
}