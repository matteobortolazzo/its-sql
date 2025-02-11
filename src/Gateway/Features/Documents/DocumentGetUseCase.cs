using System.Net;
using System.Text.Json.Nodes;
using Gateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.Features.Documents;

public static class DocumentGetUseCase
{
    public static RouteGroupBuilder MapGetDocument(this RouteGroupBuilder documentEndpoints)
    {
        documentEndpoints.MapGet("{documentId}", async (
                DockerService dockerService,
                EngineService engineService,
                PartitionService partitionService,
                [FromRoute] string container,
                [FromRoute] string documentId,
                [FromQuery] string partitionKeyValue) =>
            {
                if (!partitionService.TryGetPartitionKeyPath(container, out _))
                {
                    return TypedResults.Problem(
                        statusCode: (int)HttpStatusCode.NotFound,
                        title: "Container not found");
                }

                await dockerService.StartEngineContainerAsync(partitionKeyValue);

                var results = await engineService.GetClient(partitionKeyValue)
                    .GetFromJsonAsync<JsonObject>($"{container}/{documentId}");
                return (IResult)TypedResults.Ok(results);
            })
            .WithName("GetDocument");

        return documentEndpoints;
    }
}