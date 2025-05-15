#region

using Conllu;
using Conllu.Enums;

#endregion

namespace RobBERT_2023_BIAS.CorpusExtractor;

public static class VerbSecondExtractor
{
    public static void Extract()
    {
        List<string> v2Sentences = new();
        List<string> noV2Sentences = new();

        var corpusSentences = ConlluParser.ParseFile(Path.Combine(Environment.CurrentDirectory, "Resources/nl_alpino-ud-train.conllu"));

        foreach (Sentence sentence in corpusSentences)
        {
            if (sentence.Tokens.Count(t => t.DepRelEnum == DependencyRelation.Nsubj) == 1 &&
                sentence.Tokens.Count(t => t.DepRelEnum == DependencyRelation.Root && t.Feats.ContainsValue("Fin")) == 1)
            {
                if (sentence.Tokens.First(t => t.DepRelEnum == DependencyRelation.Root).Id <
                    sentence.Tokens.First(t => t.DepRelEnum == DependencyRelation.Nsubj).Id)
                    v2Sentences.Add(sentence.Serialize());
                else
                    noV2Sentences.Add(sentence.Serialize());
            }
        }

        var v2File = File.CreateText(Path.Combine(Environment.CurrentDirectory, "v2Sentences.conllu"));
        var noV2File = File.CreateText(Path.Combine(Environment.CurrentDirectory, "noV2Sentences.conllu"));

        using (v2File)
            foreach (string sentence in v2Sentences)
                v2File.WriteLine(sentence);

        using (noV2File)
            foreach (string sentence in noV2Sentences)
                noV2File.WriteLine(sentence);
    }
}