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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowOrigins", policyBuilder =>
    {
        // TODO: IMPORTANT change to secure specific policy https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-9.0
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
}

// app.UseHttpsRedirection();
app.UseCors("AllowOrigins");

PrepareDirectoryStructure();

List<IRobbert> robbertInstances = new();
List<RobbertSession> robbertSessions = new();
int batchProgess = 0;

Timer? idleTimer = null;


app.MapPost("robbert/beginsession", async (int version, string clientGuid) =>
{
    var robbertVersion = (RobbertVersion)version;

    Console.WriteLine(
        $"New {robbertVersion} session requested for client {clientGuid}"); // TODO: throw when creating more than 1 session per version per client

    robbertSessions.Add(new RobbertSession()
        { Version = robbertVersion, ClientGuid = clientGuid, CreationTime = DateTimeOffset.Now, LastClientRequest = DateTimeOffset.Now });

    idleTimer ??= new Timer(CheckIdleSessions, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    Console.WriteLine("New idle timer created");
    
    if (!robbertInstances.Exists(r => r.Version == robbertVersion))
    {
        try
        {
            await CreateRobbertInstance(robbertVersion);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to create Robbert instance: {ex}");
        }
    }
    else
    {
        Console.WriteLine($"No new {robbertVersion} instance created: instance already exists");
    }

    Console.WriteLine(
        $"New {robbertVersion} session added for client {clientGuid}: {robbertSessions.Count(s => s.Version == robbertVersion)} sessions and {robbertInstances.Count(i => i.Version == robbertVersion)} instances of version {robbertVersion} now active ({robbertSessions.Count} sessions and {robbertInstances.Count} instances accross all versions)");

    return Results.Ok();
});

app.MapPost("robbert/pingsession", (RobbertVersion version, string clientGuid) => UpdateLastRequestTime(version, clientGuid));

app.MapPost("robbert/process", async ([FromBody] OnlineRobbert.OnlineRobbertProcessParameters parameters, string clientGuid) =>
{
    Console.WriteLine($"Processing requested for version {parameters.Version} on client {clientGuid}");

    UpdateLastRequestTime(parameters.Version, clientGuid);
    
    if (robbertInstances.Exists(r => r.Version == parameters.Version))
    {
        var robbert = robbertInstances.First(r => r.Version == parameters.Version);
        var result = await robbert.Process(parameters.UserInput, parameters.KCount, parameters.MaskToken, parameters.CalculateProbability);
        return Results.Json(result, JsonSerializerOptions.Default, null, 200);
    }

    return Results.BadRequest($"Unable to process request: no {parameters.Version} RobBERT instance to process data");
});

app.MapPost("robbert/processbatch", async ([FromBody] OnlineRobbert.OnlineRobbertProcessBatchParameters parameters, string clientGuid) =>
{
    Console.WriteLine($"Batch processing requested for version {parameters.Version} on client {clientGuid}");

    UpdateLastRequestTime(parameters.Version, clientGuid);

    if (robbertInstances.Exists(r => r.Version == parameters.Version))
    {
        var robbert = robbertInstances.First(r => r.Version == parameters.Version);
        
        robbert.BatchProgressChanged += UpdateProgress;
        var result = await robbert.ProcessBatch(parameters.UserInput, parameters.KCount, parameters.CalculateProbability);
        robbert.BatchProgressChanged -= UpdateProgress;

        return Results.Json(result, JsonSerializerOptions.Default, null, 200);
    }

    return Results.BadRequest($"Unable to process request: no {parameters.Version} RobBERT instance to process data");
});

app.MapGet("robbert/processbatch/getprogress", () => Results.Ok(batchProgess));

app.MapDelete("robbert/endsessions", (string clientGuid) =>
{
    Console.WriteLine($"Termination of all sessions for client {clientGuid} requested");

    if (!robbertSessions.Exists(t => t.ClientGuid == clientGuid))
        return Results.BadRequest($"Unable to end session: no sessions for client {clientGuid} exist");

    foreach (var sessionToRemove in robbertSessions.Where(t => t.ClientGuid == clientGuid).ToList())
    {
        try
        {
            RemoveRobbertSession(sessionToRemove);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to end session: {ex}");
        }
    }

    Console.WriteLine(
        $"All sessions for client {clientGuid} ended: {robbertSessions.Count} sessions and {robbertInstances.Count} instances now active accross all versions");

    return Results.Ok();
});

app.Run();

async Task CreateRobbertInstance(RobbertVersion version)
{
    if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlobResources", GetDirectoryName(version, false), "model.onnx")) ||
        !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlobResources", GetDirectoryName(version, false), "tokenizer.json")))
    {
        var containerUri = GetSas(version);

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
    robbertInstances.Add(await robbertFactory.Create(version, true));
}

void RemoveRobbertSession(RobbertSession session)
{
    var sessionCount = robbertSessions.Count(s => s.Version == session.Version);

    if (sessionCount > 1)
    {
        robbertSessions.Remove(session);
        Console.WriteLine($"Session of version {session.Version} for client {session.ClientGuid} ended");
    }
    else if (sessionCount == 1)
    {
        robbertSessions.Remove(session);
        Console.WriteLine($"Session of version {session.Version} for client {session.ClientGuid} ended");

        DisposeRobbertInstance(session.Version);
        Console.WriteLine($"Instance of version {session.Version} disposed");
    }
    else
    {
        throw new InvalidOperationException($"Unable to end session: no sessions of version {session.Version} exist");
    }
}

void DisposeRobbertInstance(RobbertVersion version)
{
    if (robbertInstances.Exists(r => r.Version == version))
    {
        var versionForDisposal = robbertInstances.First(r => r.Version == version);

        if (robbertInstances.Remove(versionForDisposal))
        {
            versionForDisposal.Dispose();

            if (robbertInstances.Count == 0)
            {
                idleTimer!.Dispose();
                Console.WriteLine("Idle timer disposed because no active instances left to check");
            }
        }
        else
        {
            throw new InvalidOperationException();
        }
    }
    else
    {
        throw new InvalidOperationException();
    }
}

void UpdateProgress(object? sender, int progress) => batchProgess = progress;

IResult UpdateLastRequestTime(RobbertVersion version, string clientGuid)
{
    Console.WriteLine($"Update to last request time for version {version} on client {clientGuid} requested");

    RobbertSession session;

    try
    {
        session = robbertSessions.First(s => s.ClientGuid == clientGuid && s.Version == version);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest("Unable to update last request time for session: session not found");
    }

    session.LastClientRequest = DateTimeOffset.Now;
    Console.WriteLine($"Updated last request time for version {version} on client {clientGuid} to {DateTimeOffset.Now}");

    return Results.Ok();
}

void CheckIdleSessions(object? state)
{
    foreach (RobbertSession session in robbertSessions.ToList())
    {
        if (DateTimeOffset.Now.Subtract(session.LastClientRequest).Duration() > TimeSpan.FromMinutes(2) ||
            DateTimeOffset.Now.Subtract(session.CreationTime).Duration() > TimeSpan.FromMinutes(20))
        {
            Console.WriteLine($"Removing idle session for version {session.Version} on client {session.ClientGuid}");
            RemoveRobbertSession(session);
        }
    }
}

Uri GetSas(RobbertVersion version)
{
    var client = new BlobServiceClient(builder.Configuration["blob-connection-string"]);

    var containerClient = client.GetBlobContainerClient(GetDirectoryName(version, true)) ?? throw new NullReferenceException();
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