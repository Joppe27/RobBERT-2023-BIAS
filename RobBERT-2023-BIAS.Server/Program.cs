#region

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

List<IRobbert> robbertInstances = new();
int batchProgess = 0;

app.MapPost("/robbert/create", async ([FromBody] RobbertVersion robbertVersion) =>
{
    Console.WriteLine("Robbert requested!");

    if (!robbertInstances.Exists(r => r.Version == robbertVersion))
    {
        var robbertFactory = new LocalRobbert.Factory();
        robbertInstances.Add(await robbertFactory.Create(robbertVersion));
    }
    else
    {
        // TODO: this is not good enough: concurrent users, closing page without requesting disposal, etc.
        Console.WriteLine("Robbert already exists: no new instance created, existing instance returned");
    }

    return Results.Created();
}).WithName("Create");

app.MapPost("/robbert/process", async ([FromBody] OnlineRobbert.OnlineRobbertProcessParameters parameters) =>
{
    Console.WriteLine("Robbert prompt processing requested!");

    if (robbertInstances.Exists(r => r.Version == parameters.Version))
    {
        var robbert = robbertInstances.First(r => r.Version == parameters.Version);
        return Results.Json(await robbert.Process(parameters.UserInput, parameters.KCount, parameters.MaskToken, parameters.CalculateProbability));
    }

    return Results.BadRequest($"Unable to process request: no {parameters.Version} RobBERT instance to process data");
}).WithName("Process");

app.MapPost("/robbert/processbatch", async ([FromBody] OnlineRobbert.OnlineRobbertProcessBatchParameters parameters) =>
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
}).WithName("ProcessBatch");

app.MapGet("/robbert/processbatch/getprogress", () => Results.Ok(batchProgess)).WithName("GetProgress");

app.MapDelete("/robbert/dispose", ([FromBody] RobbertVersion version) =>
{
    Console.WriteLine("Robbert disposal requested!");

    if (robbertInstances.Exists(r => r.Version == version))
    {
        robbertInstances.First(r => r.Version == version).Dispose();

        return Results.NoContent();
    }

    return Results.BadRequest($"Unable to process request: no {version} RobBERT instance to dispose");
}).WithName("Dispose");

app.Run();

void UpdateProgress(object? sender, int progress) => batchProgess = progress;