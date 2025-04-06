#region

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using RobBERT_2023_BIAS.Inference;
using FromBodyHttp = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

#endregion

namespace RobBERT_2023_BIAS.Azure.Functions;

public class Create(RobbertManager robbertManager)
{
    [Function("Create")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBodyHttp] RobbertVersion robbertVersion)
    {
        await robbertManager.Create(robbertVersion);

        return new AcceptedResult();
    }
}