namespace RobBERT_2023_BIAS.Inference.Demos;

public class DemoJouJouw
{
    public async Task<string[]> Process(Robbert robbert, string userInput)
    {
        string[] possiblePronouns = ["jou", "jouw", "u", "uw"];
        string[] politePronouns = ["u", "uw"];
        string[] familiarPronouns = ["jou", "jouw"];
        
        List<(string Pronoun, bool PoliteForm)> userPronoun = new();
        List<(string Pronoun, float Confidence)> modelPronoun = new();

        string[] split = userInput.Split(' ', StringSplitOptions.TrimEntries);
        for (var i = 0; i < split.Length; i++)
        {
            if (possiblePronouns.Contains(split[i], StringComparer.CurrentCultureIgnoreCase))
            {
                userPronoun.Add((split[i], politePronouns.Contains(split[i], StringComparer.CurrentCultureIgnoreCase)));
                split[i] = "<mask>";
            }
        }

        string modelPrompt = String.Join(' ', split);

        // kCount = 200 as this is sufficient to calculate an answer in most cases and keeps loading times short for this demo.
        List<Dictionary<string, float>> modelOutput = await robbert.Prompt(modelPrompt, 200);

        for (int mask = 0; mask < modelOutput.Count; mask++)
        {
            for (int i = 0; mask < modelOutput[mask].Count; i++)
            {
                string pronoun = "";
                string currentCandidateToken = modelOutput[mask].Keys.ElementAt(i);
                float currentCandidateProbability = modelOutput[mask].Values.ElementAt(i);

                if (userPronoun[mask].PoliteForm && politePronouns.Contains(currentCandidateToken, StringComparer.CurrentCultureIgnoreCase))
                    pronoun = currentCandidateToken;
                else if (!userPronoun[mask].PoliteForm && familiarPronouns.Contains(currentCandidateToken, StringComparer.CurrentCultureIgnoreCase))
                    pronoun = currentCandidateToken;

                if (pronoun == "" && i < modelOutput[mask].Count - 1)
                    continue;
                
                // If none of the pronouns are found in the dictionary (i.e. when kCount isn't large enough), create empty tuple.
                if (pronoun == "" && i == modelOutput[mask].Count - 1)
                {
                    modelPronoun.Add((String.Empty, -1));
                    break;
                }

                float incorrectModelConfidence = modelOutput[mask].GetValueOrDefault(userPronoun[mask].PoliteForm ? politePronouns.First(p => p != currentCandidateToken) : familiarPronouns.First(p => p != currentCandidateToken), 0);
                float total = currentCandidateProbability + incorrectModelConfidence;
                float confidence = currentCandidateProbability / total * 100;

                modelPronoun.Add((pronoun, confidence));
                break;
            }
        }

        string[] answer = new string[modelPronoun.Count];
        
        for (var i = 0; i < modelPronoun.Count; i++)
        {
            // Warn user that kCount wasn't large enough to calculate an answer.
            if (modelPronoun[i].Confidence < 0)
            {
                answer[i] = $"Het {i + 1}{(i == 0 ? "ste" : "de")} voornaamwoord werd niet berekend om de laadtijd te beperken.";
                break;
            }
            
            answer[i] = String.Format("Het {0}{1} voornaamwoord is {2}. Het correcte voornaamwoord is {3} (met {4}% zekerheid).",
                i + 1,
                i == 0 ? "ste" : "de",
                userPronoun[i].Pronoun.Equals(modelPronoun[i].Pronoun, StringComparison.CurrentCultureIgnoreCase) ? "juist" : "fout",
                modelPronoun[i].Pronoun,
                Math.Round(modelPronoun[i].Confidence, 2));
        }

        return answer;
    }
}