using System.Net;
using System.Text.Json.Nodes;
using Gateway.Interpreter;
using Gateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.Features.Containers;

[Serializable]
public record SqlRequest(string Sql);

public static class ContainerQueryUseCase
{
    public static RouteGroupBuilder MapQueryContainer(this RouteGroupBuilder containerEndpoints)
    {
        containerEndpoints.MapPost("/{container}/query", async (
                DockerService dockerService,
                EngineService engineService,
                PartitionService partitionService,
                QueryService queryService,
                Parser parser,
                [FromRoute] string container, 
                [FromBody] SqlRequest request) =>
            {
                if (!partitionService.TryGetPartitionKeyPath(container, out var partitionKeyPath))
                {
                    return TypedResults.Problem(
                        statusCode: (int)HttpStatusCode.NotFound,
                        title: "Container not found");
                }

                var ast = parser.Parse(request.Sql);
                var partitionKeyValue = queryService.GetPartitionKeyValue(ast, partitionKeyPath!);
                if (partitionKeyValue == null)
                {
                    return TypedResults.Problem(
                        statusCode: (int)HttpStatusCode.BadRequest,
                        title: "Partition key not found");
                }

                await dockerService.StartEngineContainerAsync(partitionKeyValue);

                var response = await engineService.GetClient(partitionKeyValue)
                    .PostAsJsonAsync($"{container}/query", ast);
                var results = await response.Content.ReadFromJsonAsync<JsonObject[]>();
                return (IResult)TypedResults.Ok(results);
            })
            .WithName("Query");

        return containerEndpoints;
    }
}