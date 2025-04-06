#region

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using RobBERT_2023_BIAS.Inference;

#endregion

namespace RobBERT_2023_BIAS.Azure.Functions;

public class PollCreate(RobbertManager robbertManager)
{
    [Function("PollCreate")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req, int version)
    {
        if (robbertManager.InstanceExists((RobbertVersion)version))
            return new OkResult();

        return new NoContentResult();
    }
}