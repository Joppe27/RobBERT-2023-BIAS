#region

using System.Net.Http.Json;
using RobBERT_2023_BIAS.Inference;

#endregion

namespace RobBERT_2023_BIAS.Browser;

public class OnlineRobbert : IRobbert
{
    private HttpClient _httpClient = null!;

    // TODO: use URI gereneration instead of hardcoded 
    private OnlineRobbert()
    {
    }

    public async Task<List<Dictionary<string, float>>> Process(string userInput, int kCount, string? maskToken, bool calculateProbability = true)
    {
        var httpResponse = await _httpClient.PostAsync("/robbert/process",
            JsonContent.Create(new RobbertProcesessParameters(userInput, kCount, maskToken, calculateProbability)));

        return await httpResponse.Content.ReadFromJsonAsync<List<Dictionary<string, float>>>() ?? throw new NullReferenceException();
    }

    public void Dispose()
    {
        _httpClient.DeleteAsync("/robbert/dispose");
    }

    public class Factory : IRobbertFactory
    {
        public async Task<IRobbert> CreateRobbert(RobbertVersion version)
        {
            var onlineRobbert = new OnlineRobbert();

            // TODO: this does not need to be here, use DI instead
            onlineRobbert._httpClient = new HttpClient() { BaseAddress = new Uri("http://localhost:5164") };

            await onlineRobbert._httpClient.PostAsync("/robbert/create", JsonContent.Create((int)version));

            return onlineRobbert;
        }
    }

    public record RobbertProcesessParameters(string UserInput, int KCount, string? MaskToken, bool CalculateProbability = true);
}