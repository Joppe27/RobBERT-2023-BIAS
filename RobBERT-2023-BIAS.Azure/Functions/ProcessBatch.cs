#region

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using RobBERT_2023_BIAS.Browser;
using FromBodyHttp = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

#endregion

namespace RobBERT_2023_BIAS.Azure.Functions;

public class ProcessBatch(RobbertManager robbertManager)
{
    [Function("ProcessBatch")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
        [FromBodyHttp] OnlineRobbert.OnlineRobbertProcessBatchParameters parameters)
    {
        Console.WriteLine("Robbert prompt batch processing requested!");

        try
        {
            return new JsonResult(await robbertManager.ProcessBatch(parameters));
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(ex);
        }
    }
}