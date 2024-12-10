namespace RobBERT_2023_BIAS;

class Program
{
    static void Main(string[] args) // TODO: replace all while loops with an actual UI that lets you switch instead of restarting the entire program
    {
        int kCount = -1;

        if (args.Contains("--demojoujouw"))
        {
            var demoJouJouw = new DemoJouJouw();

            while (true)
            {
                demoJouJouw.Run();
            }
            return;
        }

        if (args.Contains("--demobias"))
        {
            var demoBias = new DemoBias();

            while (true)
            {
                demoBias.Run();
            }
            return;
        }
        
        if (args.Contains("--kcount"))
        {
            foreach (string arg in args)
            {
                int.TryParse(arg, out kCount);
            }
        }

        RunBasicMlm(kCount);
    }

    private static void RunBasicMlm(int kCount)
    {
        var prompter = new Robbert();

        while (true)
        {
            string? prompt;
            bool invalidPrompt;
            
            do
            {
                invalidPrompt = false;
            
                Console.WriteLine("Enter prompt (don't forget a <mask>):");
                prompt = Console.ReadLine();

                if (prompt == null || !prompt.Contains("<mask>"))
                {
                    invalidPrompt = true;
                    Console.WriteLine("Invalid prompt!");
                }
            } while (invalidPrompt);
        
            prompter.Prompt(prompt!, kCount == -1 ? 3 : kCount);
        }
    }
}