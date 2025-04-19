#region

using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RobBERT_2023_BIAS.Inference;
using RobBERT_2023_BIAS.Utilities;

#endregion

namespace RobBERT_2023_BIAS.Browser;

public class OnlineRobbert : IRobbert
{
    public RobbertVersion Version { get; private set; }
    
    private HttpClient _httpClient = null!;

    private Timer _idleTimer = null!;
    
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
        var httpResponse = await _httpClient.PostAsync($"robbert/process?clientGuid={App.Guid.ToString()}",
            JsonContent.Create(new OnlineRobbertProcessParameters(userInput, kCount, maskToken, Version, calculateProbability)));

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"HTTP request failed with status code {httpResponse.StatusCode}: {await httpResponse.Content.ReadAsStringAsync()}");
        
        return await httpResponse.Content.ReadFromJsonAsync<List<Dictionary<string, float>>>() ?? throw new NullReferenceException();
    }

    public async Task<List<List<Dictionary<string, float>>>> ProcessBatch(List<RobbertPrompt> userInput, int kCount,
        bool calculateProbability = true)
    {
        var httpResponse = _httpClient.PostAsync($"robbert/processbatch?clientGuid={App.Guid.ToString()}",
            JsonContent.Create(new OnlineRobbertProcessBatchParameters(userInput, kCount, Version, calculateProbability)));

        while (!httpResponse.IsCompleted)
            await PollBatchProgress();

        if (!httpResponse.Result.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"HTTP request failed with status code {httpResponse.Result.StatusCode}: {await httpResponse.Result.Content.ReadAsStringAsync()}");

        return await httpResponse.Result.Content.ReadFromJsonAsync<List<List<Dictionary<string, float>>>>() ?? throw new NullReferenceException();
    }

    public void Dispose()
    {
        var httpResponse = _httpClient.DeleteAsync($"robbert/endsessions?clientGuid={App.Guid.ToString()}").ConfigureAwait(false).GetAwaiter().GetResult();

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"HTTP request failed with status code {httpResponse.StatusCode}: {httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult()}");

        _idleTimer.Dispose();
    }

    private async Task PollBatchProgress()
    {
        var httpResponse = await _httpClient.GetAsync("robbert/processbatch/getprogress");

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"HTTP request failed with status code {httpResponse.StatusCode}: {await httpResponse.Content.ReadAsStringAsync()}");
        
        int.TryParse(await httpResponse.Content.ReadAsStringAsync(), out int currentProgress);
        
        BatchProgress = currentProgress;

        await Task.Delay(2000);
    }

    private async void PingServer(object? state)
    {
        var httpResponse = await _httpClient.PostAsync($"robbert/pingsession?version={(int)Version}&clientGuid={App.Guid}", null);

        try
        {
            if (!httpResponse.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"HTTP request failed with status code {httpResponse.StatusCode}: {await httpResponse.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex)
        {
            ExceptionUtilities.LogNotify(null, ex);
            await _idleTimer.DisposeAsync();
        }
    }

    public class Factory : IRobbertFactory
    {
        public async Task<IRobbert> Create(RobbertVersion version, bool usingBlobs = false)
        {
            var onlineRobbert = new OnlineRobbert();

            if (usingBlobs)
                throw new InvalidOperationException();
            
            onlineRobbert._httpClient = App.ServiceProvider.GetRequiredService<HttpClient>();

            var httpResponse = await onlineRobbert._httpClient.PostAsync($"robbert/beginsession?version={(int)version}&clientGuid={App.Guid.ToString()}", null);

            if (!httpResponse.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"HTTP request failed with status code {httpResponse.StatusCode}: {await httpResponse.Content.ReadAsStringAsync()}");

            onlineRobbert._idleTimer = new Timer(onlineRobbert.PingServer, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            onlineRobbert.Version = version;
            
            return onlineRobbert;
        }
    }

    public record OnlineRobbertProcessParameters(string UserInput, int KCount, string? MaskToken, RobbertVersion Version, bool CalculateProbability = true);

    public record OnlineRobbertProcessBatchParameters(List<RobbertPrompt> UserInput, int KCount, RobbertVersion Version, bool CalculateProbability = true);
}