using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.Features.Documents;

public static class DocumentUpsertUseCase
{
    public static RouteGroupBuilder MapUpsertDocument(this RouteGroupBuilder documentEndpoints)
    {
        documentEndpoints.MapPut("/{container}", async (
                DockerService dockerService,
                EngineService engineService,
                PartitionService partitionService,
                [FromRoute] string container,
                [FromBody] JsonObject jsonObject) =>
            {
                if (!partitionService.TryGetPartitionKeyPath(container, out var partitionKeyPath))
                {
                    return TypedResults.Problem(
                        statusCode: (int)HttpStatusCode.NotFound,
                        title: "Container not found");
                }
                
                var partitionKeyValue = jsonObject[partitionKeyPath!]!.GetValue<string>();

                await dockerService.StartEngineContainerAsync(partitionKeyValue);
                
                var body = JsonSerializer.Serialize(jsonObject);
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await engineService.GetClient(partitionKeyValue).PutAsync(container, content);
                return (IResult)TypedResults.Ok(response.ReasonPhrase);
            })
            .WithName("UpsertDocument");
        
        return documentEndpoints;
    }
}