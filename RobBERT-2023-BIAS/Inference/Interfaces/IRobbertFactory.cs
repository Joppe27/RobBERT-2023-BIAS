namespace RobBERT_2023_BIAS.Inference;

public interface IRobbertFactory
{
    Task<IRobbert> Create(RobbertVersion version);
}