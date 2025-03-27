#region

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using RobBERT_2023_BIAS.Inference;
using FromBodyHttp = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

#endregion

namespace RobBERT_2023_BIAS.Azure.Functions;

public class RobbertCreate(RobbertManager robbertManager)
{
    [Function("create")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBodyHttp] RobbertVersion robbertVersion)
    {
        Console.WriteLine("Robbert requested!");

        await robbertManager.Create(robbertVersion);

        return new CreatedResult();
    }
}