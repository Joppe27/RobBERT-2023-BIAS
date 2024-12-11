namespace RobBERT_2023_BIAS.Inference;

public struct RobbertInput
{
    public long[] InputIds { get; set; }
    public long[] AttentionMask { get; set; }
}