#region

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using RobBERT_2023_BIAS.Browser;
using FromBodyHttp = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

#endregion

namespace RobBERT_2023_BIAS.Azure.Functions;

public class Process(RobbertManager robbertManager)
{
    [Function("Process")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
        [FromBodyHttp] OnlineRobbert.OnlineRobbertProcessParameters parameters)
    {
        Console.WriteLine("Robbert prompt processing requested!");

        try
        {
            return new JsonResult(await robbertManager.Process(parameters));
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(ex);
        }
    }
}