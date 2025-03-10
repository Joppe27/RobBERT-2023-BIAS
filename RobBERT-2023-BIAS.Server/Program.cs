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

IRobbert? robbert = null;

app.MapPost("/robbert/create", async ([FromBody] RobbertVersion robbertVersion) =>
{
    Console.WriteLine("Robbert requested!");

    if (robbert == null)
    {
        var robbertFactory = new DesktopRobbert.Factory();
        robbert = await robbertFactory.CreateRobbert(robbertVersion);
    }
    else
    {
        // TODO: this is not good enough: concurrent users, closing page without requesting disposal, etc.
        Console.WriteLine("Robbert already exists: no new instance created, existing instance returned");
    }

    return Results.Created();
}).WithName("Create");

app.MapPost("/robbert/process", async ([FromBody] OnlineRobbert.RobbertProcesessParameters parameters) =>
{
    Console.WriteLine("Robbert prompt processing requested!");

    if (robbert != null)
        return Results.Json(await robbert.Process(parameters.UserInput, parameters.KCount, parameters.MaskToken, parameters.CalculateProbability));

    return Results.BadRequest("Unable to process request: no RobBERT instance to process data");
}).WithName("Process");

app.MapDelete("/robbert/dispose", () =>
{
    Console.WriteLine("Robbert disposal requested!");

    if (robbert != null)
    {
        robbert.Dispose();

        return Results.NoContent();
    }

    return Results.BadRequest("Unable to process request: no RobBERT instance to dispose");
}).WithName("Dispose");

app.Run();