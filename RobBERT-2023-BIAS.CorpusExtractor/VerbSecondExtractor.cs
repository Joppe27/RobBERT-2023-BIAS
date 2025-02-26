#region

using Conllu;
using Conllu.Enums;

#endregion

namespace RobBERT_2023_BIAS.CorpusExtractor;

public static class VerbSecondExtractor
{
    public static void Extract()
    {
        List<string> svoSentences = new();
        List<string> saiSentences = new();

        var corpusSentences = ConlluParser.ParseFile(Path.Combine(Environment.CurrentDirectory, "Resources/nl_alpino-ud-train.conllu"));

        foreach (Sentence sentence in corpusSentences)
        {
            if (sentence.Tokens.Count(t => t.DepRelEnum == DependencyRelation.Nsubj) == 1 &&
                sentence.Tokens.Count(t => t.DepRelEnum == DependencyRelation.Aux && t.Feats.ContainsValue("Fin")) == 1)
            {
                if (sentence.Tokens.First(t => t.DepRelEnum == DependencyRelation.Aux).Id >
                    sentence.Tokens.First(t => t.DepRelEnum == DependencyRelation.Nsubj).Id)
                    svoSentences.Add(sentence.Serialize());
                else
                    saiSentences.Add(sentence.Serialize());
            }
        }

        var svoFile = File.CreateText(Path.Combine(Environment.CurrentDirectory, "svoSentences.conllu"));
        var saiFile = File.CreateText(Path.Combine(Environment.CurrentDirectory, "saiSentences.conllu"));

        using (svoFile)
            foreach (string sentence in svoSentences)
                svoFile.WriteLine(sentence);

        using (saiFile)
            foreach (string sentence in saiSentences)
                saiFile.WriteLine(sentence);
    }
}