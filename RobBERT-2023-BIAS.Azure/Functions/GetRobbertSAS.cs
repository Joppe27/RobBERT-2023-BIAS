#region

using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using RobBERT_2023_BIAS.Inference;

#endregion

namespace RobBERT_2023_BIAS.Azure.Functions;

public class GetRobbertSAS
{
    [Function("getrobbertsas")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req, int version)
    {
        var client = new BlobServiceClient(Environment.GetEnvironmentVariable("BLOB_CONNECTION_STRING"));

        string containerName;

        switch ((RobbertVersion)version)
        {
            case RobbertVersion.Base2022:
                containerName = "robbert2022base";
                break;
            case RobbertVersion.Base2023:
                containerName = "robbert2023base";
                break;
            case RobbertVersion.Large2023:
                containerName = "robbert2023large";
                break;
            default:
                throw new InvalidOperationException("Unsupported RobBERT version requested");
        }

        var containerClient = client.GetBlobContainerClient(containerName) ?? throw new NullReferenceException();
        var containerSas = containerClient.GenerateSasUri(BlobContainerSasPermissions.Read, DateTimeOffset.Now.AddMinutes(1));

        return new JsonResult(containerSas);
    }
}