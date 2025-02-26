#region

using RobBERT_2023_BIAS.CorpusExtractor;

#endregion

if (args.Length == 0)
    throw new ArgumentOutOfRangeException(nameof(args), "No program arguments specified");

foreach (string arg in args)
{
    switch (arg)
    {
        case "--verbsecond":
            VerbSecondExtractor.Extract();
            break;
        case "--subjectauxiliary":
            break;
        case "--genderedpronouns":
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(arg), "Invalid program argument specified");
    }
}

