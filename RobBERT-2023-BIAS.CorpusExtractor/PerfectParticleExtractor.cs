#region

using Conllu;
using Conllu.Enums;

#endregion

namespace RobBERT_2023_BIAS.CorpusExtractor;

public class PerfectParticleExtractor
{
    public static void Extract()
    {
        List<string> perfectEndSentences = new();
        List<string> perfectSuccessiveSentences = new();

        var corpusSentences = ConlluParser.ParseFile(Path.Combine(Environment.CurrentDirectory, "Resources/nl_alpino-ud-train.conllu"));

        foreach (Sentence sentence in corpusSentences)
        {
            var auxTokens = sentence.Tokens.Where(t => t.UposEnum == PosTag.Aux).ToArray();
            var verbTokens = sentence.Tokens.Where(t => t.UposEnum == PosTag.Verb).ToArray();

            Dictionary<Token, Token> auxVerbPairs = new();

            foreach (Token token in auxTokens)
            {
                if (token.Head is { } auxHead && verbTokens.Select(v => v.Id).Contains(auxHead))
                    auxVerbPairs.Add(token, verbTokens.First(t => t.Id == auxHead));
            }

            if (auxVerbPairs.Count == 1 && auxVerbPairs.Keys.First().Feats.ContainsValue("Past"))
            {
                if (Math.Abs(auxVerbPairs.Keys.First().Id - auxVerbPairs.Values.First().Id) > 1)
                    perfectEndSentences.Add(sentence.Serialize());
                else
                    perfectSuccessiveSentences.Add(sentence.Serialize());
            }
        }

        var perfectEndFile = File.CreateText(Path.Combine(Environment.CurrentDirectory, "perfectEndSentences.conllu"));
        var perfectSuccessiveFile = File.CreateText(Path.Combine(Environment.CurrentDirectory, "perfectSuccessiveSentences.conllu"));

        using (perfectEndFile)
            foreach (string sentence in perfectEndSentences)
                perfectEndFile.WriteLine(sentence);

        using (perfectSuccessiveFile)
            foreach (string sentence in perfectSuccessiveSentences)
                perfectSuccessiveFile.WriteLine(sentence);
    }
}