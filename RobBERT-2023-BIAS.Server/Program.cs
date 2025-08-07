// Copyright (c) Joppe27 <joppe27.be>. Licensed under the MIT Licence.
// See LICENSE file in repository root for full license text.

#region

using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Mvc;
using RobBERT_2023_BIAS.Browser;
using RobBERT_2023_BIAS.Inference;

#endregion

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Logging.AddConsole();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", policyBuilder =>
    {
        policyBuilder
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("AllowAllOrigins");
}
else
{
    app.UseHttpsRedirection();
}

PrepareDirectoryStructure();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

Dictionary<IRobbert, SemaphoreSlim> robbertInstances = new();
List<RobbertSession> robbertSessions = new();
Dictionary<RobbertSession, int> batchProgess = new();
Dictionary<RobbertSession, CancellationTokenSource> batchCancellationTokenSource = new();

Timer? idleTimer = null;

app.MapPost("robbert/beginsession", async (int version, string clientGuid) =>
{
    var robbertVersion = (RobbertVersion)version;

    logger.LogInformation(
        $"New {robbertVersion} session requested for client {clientGuid}");

    robbertSessions.Add(new RobbertSession()
        { Version = robbertVersion, ClientGuid = clientGuid, CreationTime = DateTimeOffset.Now, LastClientRequest = DateTimeOffset.Now });

    idleTimer ??= new Timer(CheckIdleSessions, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    logger.LogInformation("New idle timer created");

    if (robbertInstances.Keys.All(r => r.Version != robbertVersion))
    {
        try
        {
            await CreateRobbertInstance(robbertVersion);
        }
        catch (Exception ex)
        {
            string message = $"Failed to create Robbert instance: {ex}";
            logger.LogError(message);
            return Results.Problem(message);
        }
    }
    else
    {
        logger.LogInformation($"No new {robbertVersion} instance created: instance already exists");
    }

    logger.LogInformation(
        $"New {robbertVersion} session added for client {clientGuid}: {robbertSessions.Count(s => s.Version == robbertVersion)} sessions and {robbertInstances.Keys.Count(i => i.Version == robbertVersion)} instances of version {robbertVersion} now active ({robbertSessions.Count} sessions and {robbertInstances.Count} instances accross all versions)");
    return Results.Ok();
});

app.MapPost("robbert/pingsession", (RobbertVersion version, string clientGuid) => UpdateLastRequestTime(version, clientGuid));

app.MapPost("robbert/process", async ([FromBody] OnlineRobbert.OnlineRobbertProcessParameters parameters, string clientGuid) =>
{
    logger.LogInformation($"Processing requested for version {parameters.Version} on client {clientGuid}");

    UpdateLastRequestTime(parameters.Version, clientGuid);

    if (robbertInstances.Keys.Any(r => r.Version == parameters.Version))
    {
        var robbert = robbertInstances.First(r => r.Key.Version == parameters.Version);

        await robbert.Value.WaitAsync();
        try
        {
            List<Dictionary<string, float>> result;

            try
            {
                result = await robbert.Key.Process(parameters.UserInput, parameters.KCount, parameters.WordToMask, parameters.WordToDecode,
                    parameters.CalculateProbability);
            }
            catch (Exception ex)
            {
                string msg = $"Failed processing: {ex}";
                logger.LogError(msg);
                return Results.Problem(msg);
            }

            return Results.Json(result, JsonSerializerOptions.Default, null, 200);
        }
        finally
        {
            robbert.Value.Release();
        }
    }

    string message = $"Unable to process request: no {parameters.Version} RobBERT instance to process data";
    logger.LogError(message);
    return Results.BadRequest(message);
});

app.MapPost("robbert/processbatch",
    async ([FromBody] OnlineRobbert.OnlineRobbertProcessBatchParameters parameters, string clientGuid) =>
    {
        logger.LogInformation($"Batch processing requested for version {parameters.Version} on client {clientGuid}");

        UpdateLastRequestTime(parameters.Version, clientGuid);

        if (robbertInstances.Keys.Any(r => r.Version == parameters.Version))
        {
            var currentSession = robbertSessions.First(s => s.Version == parameters.Version && s.ClientGuid == clientGuid);

            batchCancellationTokenSource[currentSession] = new CancellationTokenSource();

            var robbert = robbertInstances.First(r => r.Key.Version == parameters.Version);

            await robbert.Value.WaitAsync();
            try
            {
                List<List<Dictionary<string, float>>> result;

                EventHandler<int> handler = (_, progress) =>
                    UpdateProgress(robbertSessions.FirstOrDefault(s => s.Version == parameters.Version && s.ClientGuid == clientGuid), progress);

                robbert.Key.BatchProgressChanged += handler;

                batchCancellationTokenSource[currentSession].Token.Register(() => robbert.Key.BatchProgressChanged -= handler);

                try
                {
                    CancellationToken cancellationToken;

                    if (batchCancellationTokenSource.TryGetValue(currentSession, out CancellationTokenSource? tokenSource))
                    {
                        cancellationToken = tokenSource.Token;
                    }
                    else
                    {
                        logger.LogError("No batch processing CancellationToken found for current session: continuing without token, cancellation impossible");
                        cancellationToken = CancellationToken.None;
                    }

                    result = await robbert.Key.ProcessBatch(parameters.UserInput, parameters.KCount, cancellationToken, parameters.CalculateProbability);
                }
                catch (Exception ex)
                {
                    string msg = $"Failed processing batch: {ex}";
                    logger.LogError(msg);
                    return Results.Problem(msg);
                }

                robbert.Key.BatchProgressChanged -= handler;

                return Results.Json(result, JsonSerializerOptions.Default, null, 200);
            }
            finally
            {
                robbert.Value.Release();
            }
        }

        string message = $"Unable to process request: no {parameters.Version} RobBERT instance to process data";
        logger.LogError(message);
        return Results.BadRequest(message);
    });

app.MapPost("robbert/processbatch/cancel", (string clientGuid) =>
{
    foreach (CancellationTokenSource token in batchCancellationTokenSource.Where(s => s.Key.ClientGuid == clientGuid).Select(kvp => kvp.Value))
        token.Cancel();
});

app.MapGet("robbert/processbatch/getprogress", (int version, string clientGuid) =>
{
    var robbertVersion = (RobbertVersion)version;

    var session = robbertSessions.FirstOrDefault(s => s.Version == robbertVersion && s.ClientGuid == clientGuid);

    if (session == null)
    {
        string message = $"Unable to get progress: no session with guid {clientGuid} active";
        logger.LogError(message);
        return Results.BadRequest(message);
    }

    if (batchProgess.TryGetValue(session, out var progress))
        return Results.Ok(progress);

    return Results.Ok(0);
});

app.MapDelete("robbert/endsession", (int version, string clientGuid) =>
{
    var robbertVersion = (RobbertVersion)version;

    logger.LogInformation($"Termination of {robbertVersion} session for client {clientGuid} requested");

    if (!robbertSessions.Exists(t => t.ClientGuid == clientGuid))
    {
        string message = $"Unable to end session: no sessions for client {clientGuid} exist";
        logger.LogError(message);
        return Results.BadRequest(message);
    }

    foreach (var sessionToRemove in robbertSessions.Where(t => t.ClientGuid == clientGuid && t.Version == robbertVersion).ToList())
    {
        try
        {
            RemoveRobbertSession(sessionToRemove);
        }
        catch (Exception ex)
        {
            string message = $"Failed to end session: {ex}";
            logger.LogError(message);
            return Results.Problem(message);
        }
    }

    logger.LogInformation(
        $"Session of version {robbertVersion} for client {clientGuid} ended: {robbertSessions.Count} sessions and {robbertInstances.Count} instances now active accross all versions");
    return Results.Ok();
});

app.Run();

async Task CreateRobbertInstance(RobbertVersion version)
{
    if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlobResources", GetDirectoryName(version, false), "model.onnx")) ||
        !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlobResources", GetDirectoryName(version, false), "tokenizer.json")))
    {
        logger.LogInformation("Acquiring SAS to start model download");
        var containerUri = GetSas(version) ?? throw new NullReferenceException();

        logger.LogInformation("Downloading model...");

        var containerClient = new BlobContainerClient(containerUri);

        var modelClient = containerClient.GetBlobClient("model.onnx");
        var modelDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlobResources", GetDirectoryName(version, false));
        var modelStream = File.Create(Path.Combine(modelDirectory, "model.onnx"));
        await modelClient.DownloadToAsync(modelStream);
        await modelStream.DisposeAsync();

        var tokenizerClient = containerClient.GetBlobClient("tokenizer.json");
        var tokenizerDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlobResources", GetDirectoryName(version, false));
        var tokenizerStream = File.Create(Path.Combine(tokenizerDirectory, "tokenizer.json"));
        await tokenizerClient.DownloadToAsync(tokenizerStream);
        await tokenizerStream.DisposeAsync();
    }

    var robbertFactory = new LocalRobbert.Factory();
    robbertInstances.Add(await robbertFactory.Create(version, true), new SemaphoreSlim(1, 1));
}

void RemoveRobbertSession(RobbertSession session)
{
    var sessionCount = robbertSessions.Count(s => s.Version == session.Version);

    if (sessionCount > 1)
    {
        robbertSessions.Remove(session);
        logger.LogInformation($"Session of version {session.Version} for client {session.ClientGuid} ended");

        if (batchProgess.ContainsKey(session))
            batchProgess.Remove(session);

        if (batchCancellationTokenSource.ContainsKey(session))
            batchCancellationTokenSource.Remove(session);
    }
    else if (sessionCount == 1)
    {
        robbertSessions.Remove(session);
        logger.LogInformation($"Session of version {session.Version} for client {session.ClientGuid} ended");

        if (batchProgess.ContainsKey(session))
            batchProgess.Remove(session);

        if (batchCancellationTokenSource.ContainsKey(session))
            batchCancellationTokenSource.Remove(session);

        DisposeRobbertInstance(session.Version);
        logger.LogInformation($"Instance of version {session.Version} disposed");
    }
    else
    {
        logger.LogError($"Unable to end session: no sessions of version {session.Version} exist");
    }
}

void DisposeRobbertInstance(RobbertVersion version)
{
    if (robbertInstances.Keys.Any(r => r.Version == version))
    {
        var instanceForDisposal = robbertInstances.Keys.First(r => r.Version == version);

        if (robbertInstances.Remove(instanceForDisposal))
        {
            instanceForDisposal.DisposeAsync();

            if (robbertInstances.Count == 0)
            {
                idleTimer!.Dispose();
                logger.LogInformation("Idle timer disposed because no active instances left to check");
            }
        }
        else
        {
            logger.LogError("Unable to dispose instance: something went VERY wrong");
        }
    }
    else
    {
        logger.LogError($"Unable to dispose instance: instance of {version} does not exist");
    }
}

void UpdateProgress(RobbertSession? session, int progress)
{
    if (session != null)
        batchProgess[session] = progress;
    else
        logger.LogError("Unable to update internal batch processing progress: session does not exist");
}

IResult UpdateLastRequestTime(RobbertVersion version, string clientGuid)
{
    logger.LogInformation($"Update to last request time for version {version} on client {clientGuid} requested");

    RobbertSession session;

    try
    {
        session = robbertSessions.First(s => s.ClientGuid == clientGuid && s.Version == version);
    }
    catch (InvalidOperationException)
    {
        string message = "Unable to update last request time for session: session not found";
        logger.LogError(message);
        return Results.BadRequest(message);
    }

    session.LastClientRequest = DateTimeOffset.Now;

    logger.LogInformation($"Updated last request time for version {version} on client {clientGuid} to {DateTimeOffset.Now}");
    return Results.Ok();
}

void CheckIdleSessions(object? state)
{
    foreach (RobbertSession session in robbertSessions.ToList())
    {
        if (DateTimeOffset.Now.Subtract(session.LastClientRequest).Duration() > TimeSpan.FromMinutes(2) ||
            DateTimeOffset.Now.Subtract(session.CreationTime).Duration() > TimeSpan.FromMinutes(20))
        {
            logger.LogInformation($"Removing idle session for version {session.Version} on client {session.ClientGuid}");
            RemoveRobbertSession(session);
        }
    }
}

Uri? GetSas(RobbertVersion version)
{
    var client = new BlobServiceClient(builder.Configuration["blob-connection-string"]);

    var containerClient = client.GetBlobContainerClient(GetDirectoryName(version, true));

    if (containerClient == null)
    {
        logger.LogError("Unable to acquire SAS: blob container not found");
        return null;
    }

    return containerClient.GenerateSasUri(BlobContainerSasPermissions.Read, DateTimeOffset.Now.AddMinutes(5));
}

void PrepareDirectoryStructure()
{
    var resourceDirectoryStructures = Directory.GetDirectories(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"));

    Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlobResources"));

    foreach (string directoryStructure in resourceDirectoryStructures)
        Directory.CreateDirectory(directoryStructure.Replace("Resources", "BlobResources"));
}

string GetDirectoryName(RobbertVersion version, bool blobContainer)
{
    string containerName;

    switch (version)
    {
        case RobbertVersion.Base2022:
            containerName = blobContainer ? "robbert2022base" : "RobBERT-2022-base";
            break;
        case RobbertVersion.Base2023:
            containerName = blobContainer ? "robbert2023base" : "RobBERT-2023-base";
            break;
        case RobbertVersion.Large2023:
            containerName = blobContainer ? "robbert2023large" : "RobBERT-2023-large";
            break;
        default:
            throw new InvalidOperationException("Unsupported RobBERT version requested");
    }

    return containerName;
}

record RobbertSession
{
    public required RobbertVersion Version { get; init; }
    public required string ClientGuid { get; init; }
    public required DateTimeOffset CreationTime { get; init; }
    public required DateTimeOffset LastClientRequest { get; set; }
};