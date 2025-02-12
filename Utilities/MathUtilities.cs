namespace RobBERT_2023_BIAS.Utilities;

public static class MathUtilities
{
    public static string RoundSignificant(float floatNumber, int significantDigits = 0)
    {
        double number = (double)floatNumber;

        // Turn probability (total = 1) into percentage (total = 100).
        number *= 100;

        // Make sure to always round to a significant figure, i.e. never round to 0.
        double tempNumber = number;
        int actualDigits = 0;

        // ActualDigits < 15 because of double precision limit.
        while (tempNumber <= Math.Pow(10, significantDigits - 1) && actualDigits < 15)
        {
            if (number > 1 || number <= 0)
            {
                actualDigits = significantDigits;
                break;
            }

            tempNumber *= 10;
            actualDigits++;
        }

        return double.Round(number, actualDigits).ToString($"F{actualDigits}");
    }
}