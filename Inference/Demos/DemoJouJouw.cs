namespace RobBERT_2023_BIAS;

public class DemoJouJouw
{
    private readonly Robbert _robbert = new Robbert();
    
    public void Run()
    {
        string? userInput;
        bool invalidInput;
        string modelPrompt = null!;

        string userPronoun = null!;
        bool? politeForm = null;
        string[] possiblePronouns = ["jou", "jouw", "u", "uw"];
        string[] politePronouns = ["u", "uw"];
        string[] familiarPronouns = ["jou", "jouw"];
        
        string modelPronoun = null!;
        float modelConfidence = -1;
        
        do
        {
            invalidInput = false;
            
            Console.WriteLine("Voer een zin in die één keer jou/jouw/u/uw bevat om het correct gebruik van het voornaamwoord te controleren:");
            userInput = Console.ReadLine();

            if (userInput == null || !possiblePronouns.Any(p => userInput.Contains(p, StringComparison.CurrentCultureIgnoreCase)))
            {
                invalidInput = true;
                Console.WriteLine("Invoer ongeldig!");
            }
        } while (invalidInput);

        foreach (var pronoun in possiblePronouns)
        {
            if (!userInput!.Contains(pronoun + " ", StringComparison.CurrentCultureIgnoreCase)) 
                continue;
            
            userPronoun = pronoun;
            modelPrompt = userInput.Replace(pronoun, "<mask>", StringComparison.CurrentCultureIgnoreCase);
            politeForm = politePronouns.Contains(pronoun, StringComparer.CurrentCultureIgnoreCase);
            break;
        }

        // kCount hardcoded to 50 in order to make sure any of the pronouns is included in the model's output.
        Dictionary<string, float> modelOutput = _robbert.Prompt(modelPrompt, 50);

        foreach (KeyValuePair<string, float> kvp in modelOutput)
        {
            if (politeForm is true && politePronouns.Contains(kvp.Key, StringComparer.CurrentCultureIgnoreCase))
                modelPronoun = kvp.Key;
            else if (politeForm is false && familiarPronouns.Contains(kvp.Key, StringComparer.CurrentCultureIgnoreCase))
                modelPronoun = kvp.Key;

            if (modelPronoun == null) 
                continue;
            
            float incorrectModelConfidence = modelOutput.GetValueOrDefault(politeForm is true ? politePronouns.First(p => p != kvp.Key) : familiarPronouns.First(p => p != kvp.Key), 0);
            float total = kvp.Value + incorrectModelConfidence;
            modelConfidence = kvp.Value / total * 100;
            break;
        }

        if (modelPronoun == null || modelConfidence < 0)
            throw new NullReferenceException();
        
        Console.WriteLine($"Die zin is {(userPronoun.Equals(modelPronoun, StringComparison.CurrentCultureIgnoreCase) ? "juist" : "fout")}. Het correcte voornaamwoord hier is \"{modelPronoun}\" (met {Math.Round(modelConfidence, 2)}% zekerheid).");
    }
}