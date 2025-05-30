// Copyright (c) Joppe27 <joppe27.be>. Licensed under the MIT Licence.
// See LICENSE file in repository root for full license text.

#region

using RobBERT_2023_BIAS.CorpusExtractor;

#endregion

if (args.Length == 0)
    throw new ArgumentOutOfRangeException(nameof(args), "No program arguments specified");

foreach (string arg in args)
{
    switch (arg)
    {
        case $"--{nameof(VerbSecondExtractor)}":
            VerbSecondExtractor.Extract();
            break;
        case $"--{nameof(SubjectAuxiliaryExtractor)}":
            SubjectAuxiliaryExtractor.Extract();
            break;
        case $"--{nameof(PerfectParticipleExtractor)}":
            PerfectParticipleExtractor.Extract();
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(arg), "Invalid program argument specified");
    }
}