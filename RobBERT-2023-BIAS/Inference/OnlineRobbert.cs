#region

using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RobBERT_2023_BIAS.Inference;

#endregion

namespace RobBERT_2023_BIAS.Browser;

public class OnlineRobbert : IRobbert
{
    public RobbertVersion Version { get; private set; }
    
    private HttpClient _httpClient = null!;
    
    private OnlineRobbert()
    {
    }

    private int _batchProgress;

    private int BatchProgress
    {
        get => _batchProgress;
        set
        {
            if (BatchProgressChanged != null && (value > BatchProgress || value == 0))
                BatchProgressChanged.Invoke(this, value);

            _batchProgress = value;
        }
    }
    
    public event EventHandler<int>? BatchProgressChanged;

    public async Task<List<Dictionary<string, float>>> Process(string userInput, int kCount, string? maskToken, bool calculateProbability = true)
    {
        var httpResponse = await _httpClient.PostAsync("Process",
            JsonContent.Create(new OnlineRobbertProcessParameters(userInput, kCount, maskToken, Version, calculateProbability)));

        return await httpResponse.Content.ReadFromJsonAsync<List<Dictionary<string, float>>>() ?? throw new NullReferenceException();
    }

    public async Task<List<List<Dictionary<string, float>>>> ProcessBatch(List<RobbertPrompt> userInput, int kCount,
        bool calculateProbability = true)
    {
        var httpResult = _httpClient.PostAsync("ProcessBatch",
            JsonContent.Create(new OnlineRobbertProcessBatchParameters(userInput, kCount, Version, calculateProbability)));

        while (!httpResult.IsCompleted)
            await PollBatchProgress();

        return await httpResult.Result.Content.ReadFromJsonAsync<List<List<Dictionary<string, float>>>>() ?? throw new NullReferenceException();
    }

    public void Dispose()
    {
        _httpClient.DeleteAsync("Dispose");
    }

    private async Task PollBatchProgress()
    {
        var httpResponse = await _httpClient.GetAsync("PollProcessBatch");
        int.TryParse(await httpResponse.Content.ReadAsStringAsync(), out int currentProgress);
        
        BatchProgress = currentProgress;

        await Task.Delay(2000);
    }

    public class Factory : IRobbertFactory
    {
        public async Task<IRobbert> Create(RobbertVersion version)
        {
            var onlineRobbert = new OnlineRobbert();

            onlineRobbert._httpClient = App.ServiceProvider.GetRequiredService<HttpClient>();
            onlineRobbert.Version = version;

            await onlineRobbert._httpClient.PostAsync("Create", JsonContent.Create((int)version));

            // bool robbertCreated = false;
            //
            // while (!robbertCreated)
            // {
            //     var httpRespose = await onlineRobbert._httpClient.GetAsync($"PollCreate?version={(int)version}");
            //     robbertCreated = httpRespose.StatusCode == HttpStatusCode.OK;
            //
            //     await Task.Delay(2000);
            // }
            
            return onlineRobbert;
        }
    }

    public record OnlineRobbertProcessParameters(string UserInput, int KCount, string? MaskToken, RobbertVersion Version, bool CalculateProbability = true);

    public record OnlineRobbertProcessBatchParameters(List<RobbertPrompt> UserInput, int KCount, RobbertVersion Version, bool CalculateProbability = true);
}