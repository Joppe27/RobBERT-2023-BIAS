#region

using RobBERT_2023_BIAS.Browser;
using RobBERT_2023_BIAS.Inference;

#endregion

namespace RobBERT_2023_BIAS.Azure;

public class RobbertManager
{
    private List<IRobbert> _robbertInstances = new();
    private int _batchProgess = 0;

    public RobbertManager()
    {
    }

    public async Task Create(RobbertVersion robbertVersion)
    {
        if (!_robbertInstances.Exists(r => r.Version == robbertVersion))
        {
            var robbertFactory = new LocalRobbert.Factory();
            _robbertInstances.Add(await robbertFactory.CreateFromBlob(robbertVersion));
        }
        else
        {
            // TODO: this is not good enough: concurrent users, closing page without requesting disposal, etc.
            Console.WriteLine("Robbert already exists: no new instance created");
        }
    }

    public async Task<List<Dictionary<string, float>>> Process(OnlineRobbert.OnlineRobbertProcessParameters parameters)
    {
        if (_robbertInstances.Exists(r => r.Version == parameters.Version))
        {
            var robbert = _robbertInstances.First(r => r.Version == parameters.Version);
            return await robbert.Process(parameters.UserInput, parameters.KCount, parameters.MaskToken, parameters.CalculateProbability);
        }

        throw new InvalidOperationException($"No {parameters.Version} RobBERT instance to process data");
    }

    public async Task<List<List<Dictionary<string, float>>>> ProcessBatch(OnlineRobbert.OnlineRobbertProcessBatchParameters parameters)
    {
        if (_robbertInstances.Exists(r => r.Version == parameters.Version))
        {
            var robbert = _robbertInstances.First(r => r.Version == parameters.Version);

            robbert.BatchProgressChanged += UpdateProgress;
            var result = await robbert.ProcessBatch(parameters.UserInput, parameters.KCount, parameters.CalculateProbability);
            robbert.BatchProgressChanged -= UpdateProgress;

            return result;
        }

        throw new InvalidOperationException($"No {parameters.Version} RobBERT instance to process data");
    }

    public int GetProgress() => _batchProgess;

    public void Dispose(RobbertVersion version)
    {
        if (_robbertInstances.Exists(r => r.Version == version))
            _robbertInstances.First(r => r.Version == version).Dispose();
        else
            throw new InvalidOperationException($"No {version} RobBERT instance to dispose");
    }

    private void UpdateProgress(object? sender, int progress) => _batchProgess = progress;
}