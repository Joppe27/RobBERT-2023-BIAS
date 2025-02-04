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

        List<Dictionary<string, float>> modelOutput = await robbert.Prompt(modelPrompt, 300);

        for (int mask = 0; mask < modelOutput.Count; mask++)
        {
            foreach (KeyValuePair<string, float> kvp in modelOutput[mask])
            {
                string pronoun = "";

                if (userPronoun[mask].PoliteForm && politePronouns.Contains(kvp.Key, StringComparer.CurrentCultureIgnoreCase))
                    pronoun = kvp.Key;
                else if (!userPronoun[mask].PoliteForm && familiarPronouns.Contains(kvp.Key, StringComparer.CurrentCultureIgnoreCase))
                    pronoun = kvp.Key;

                if (pronoun == "")
                    continue;

                float incorrectModelConfidence = modelOutput[mask].GetValueOrDefault(userPronoun[mask].PoliteForm ? politePronouns.First(p => p != kvp.Key) : familiarPronouns.First(p => p != kvp.Key), 0);
                float total = kvp.Value + incorrectModelConfidence;
                float confidence = kvp.Value / total * 100;

                modelPronoun.Add((pronoun, confidence));
                break;
            }
        }

        if (modelPronoun.Count < modelOutput.Count || modelPronoun.Exists(p => p.Confidence < 0))
            throw new Exception("TODO: if model doesn't predict any of the pronouns, display error message in UI");

        string[] answer = new string[modelPronoun.Count];
        
        for (var i = 0; i < modelPronoun.Count; i++)
        {
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