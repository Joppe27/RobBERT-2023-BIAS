namespace RobBERT_2023_BIAS;

class Program
{
    static void Main(string[] args)
    {
        int kCount = 0;

        if (args.Contains("--kcount"))
        {
            foreach (string arg in args)
            {
                int.TryParse(arg, out kCount);
            }
        }
        
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
        
            prompter.Prompt(prompt!, kCount == 0 ? 3 : kCount);
        }
    }
}