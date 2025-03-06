#region

using RobBERT_2023_BIAS.Browser;
using RobBERT_2023_BIAS.Inference;

#endregion

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection();

IRobbert? robbert = null;

app.MapPost("/robbert/create", async (RobbertVersion version) =>
{
    Console.WriteLine("Robbert requested!");

    var robbertFactory = new DesktopRobbert.Factory();
    robbert = await robbertFactory.CreateRobbert(version);
}).WithName("Create");

app.MapPost("/robbert/process", async (OnlineRobbert.RobbertProcesessParameters parameters) =>
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