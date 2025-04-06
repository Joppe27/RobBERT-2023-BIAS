#region

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using RobBERT_2023_BIAS.Inference;
using FromBodyHttp = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

#endregion

namespace RobBERT_2023_BIAS.Azure.Functions;

public class Dispose(RobbertManager robbertManager)
{
    [Function("Dispose")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequest req, [FromBodyHttp] RobbertVersion robbertVersion)
    {
        Console.WriteLine("Robbert disposal requested!");

        try
        {
            robbertManager.Dispose(robbertVersion);
            return new NoContentResult();
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(ex);
        }
    }
}