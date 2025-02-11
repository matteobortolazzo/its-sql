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
                [FromRoute] string container,
                [FromRoute] string documentId,
                [FromQuery] string partitionKeyValue) =>
            {
                await dockerService.StartEngineContainerAsync(partitionKeyValue);

                var results = await engineService.GetClient(partitionKeyValue)
                    .GetFromJsonAsync<JsonObject>($"{container}/{documentId}");
                return TypedResults.Ok(results);
            })
            .WithName("GetDocument");
        
        return documentEndpoints;
    }
}