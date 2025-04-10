#region

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
int batchProgess = 0;

app.MapPost("robbert/create", async ([FromBody] RobbertVersion robbertVersion) =>
{
    Console.WriteLine("Robbert requested!");

    if (!robbertInstances.Exists(r => r.Version == robbertVersion))
    {
        if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlobResources", GetDirectoryName(robbertVersion, false), "model.onnx")) ||
            !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlobResources", GetDirectoryName(robbertVersion, false), "tokenizer.json")))
        {
            var containerUri = GetSas(robbertVersion);

            var containerClient = new BlobContainerClient(containerUri);

            var modelClient = containerClient.GetBlobClient("model.onnx");
            var modelDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlobResources", GetDirectoryName(robbertVersion, false));
            var modelStream = File.Create(Path.Combine(modelDirectory, "model.onnx"));
            await modelClient.DownloadToAsync(modelStream);
            await modelStream.DisposeAsync();

            var tokenizerClient = containerClient.GetBlobClient("tokenizer.json");
            var tokenizerDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlobResources", GetDirectoryName(robbertVersion, false));
            var tokenizerStream = File.Create(Path.Combine(tokenizerDirectory, "tokenizer.json"));
            await tokenizerClient.DownloadToAsync(tokenizerStream);
            await tokenizerStream.DisposeAsync();
        }
        
        var robbertFactory = new LocalRobbert.Factory();
        robbertInstances.Add(await robbertFactory.Create(robbertVersion, true));
    }
    else
    {
        // TODO: this is not good enough: concurrent users, closing page without requesting disposal, etc.
        Console.WriteLine("Robbert already exists: no new instance created, existing instance returned");
    }

    return Results.Created();
});

app.MapPost("robbert/process", async ([FromBody] OnlineRobbert.OnlineRobbertProcessParameters parameters) =>
{
    Console.WriteLine("Robbert prompt processing requested!");

    if (robbertInstances.Exists(r => r.Version == parameters.Version))
    {
        var robbert = robbertInstances.First(r => r.Version == parameters.Version);
        return Results.Json(await robbert.Process(parameters.UserInput, parameters.KCount, parameters.MaskToken, parameters.CalculateProbability));
    }

    return Results.BadRequest($"Unable to process request: no {parameters.Version} RobBERT instance to process data");
});

app.MapPost("robbert/processbatch", async ([FromBody] OnlineRobbert.OnlineRobbertProcessBatchParameters parameters) =>
{
    Console.WriteLine("Robbert prompt batch processing requested!");

    if (robbertInstances.Exists(r => r.Version == parameters.Version))
    {
        var robbert = robbertInstances.First(r => r.Version == parameters.Version);
        
        robbert.BatchProgressChanged += UpdateProgress;
        var result = await robbert.ProcessBatch(parameters.UserInput, parameters.KCount, parameters.CalculateProbability);
        robbert.BatchProgressChanged -= UpdateProgress;
        
        return Results.Json(result);
    }

    return Results.BadRequest($"Unable to process request: no {parameters.Version} RobBERT instance to process data");
});

app.MapGet("robbert/processbatch/getprogress", () => Results.Ok(batchProgess));

app.MapDelete("robbert/dispose", (int version) =>
{
    Console.WriteLine("Robbert disposal requested!");

    if (robbertInstances.Exists(r => r.Version == (RobbertVersion)version))
    {
        var versionForDisposal = robbertInstances.First(r => r.Version == (RobbertVersion)version);

        if (robbertInstances.Remove(versionForDisposal))
            versionForDisposal.Dispose();

        return Results.NoContent();
    }

    return Results.BadRequest($"Unable to process request: no {version} RobBERT instance to dispose");
});

app.Run();

void UpdateProgress(object? sender, int progress) => batchProgess = progress;

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