using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Docker.DotNet;
using Docker.DotNet.Models;
using Gateway.Interpreter;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var containers = new Dictionary<string, string>()
{
    { "users", "city" }
};

var dockerHostUri = new Uri(Environment.GetEnvironmentVariable("DOCKER_HOST")!);
var dockerClient = new DockerClientConfiguration(dockerHostUri).CreateClient();

app.MapPost("/{container}/query", async (
        IHttpClientFactory httpClientFactory,
        [FromRoute] string container, [FromBody] SqlRequest request) =>
    {
        var lexer = new Lexer();
        var tokens = lexer.Tokenize(request.Sql);
        var parser = new Parser();
        var ast = parser.Parse(tokens);
        var partitionKeyPath = containers[container];
        var extractor = new PartitionKeyExtractor(partitionKeyPath);
        var partitionKeyValue = extractor.Get(ast);
        if (partitionKeyValue == null)
        {
            return TypedResults.Problem(
                statusCode: (int)HttpStatusCode.BadRequest,
                title: "Partition key not found");
        }
        
        await StartEngineContainerAsync(dockerClient, app.Logger, partitionKeyValue);
        
        var response = await GetClient(httpClientFactory, partitionKeyValue).PostAsJsonAsync($"{container}/query", ast);
        var results = await response.Content.ReadFromJsonAsync<JsonObject[]>();
        return (IResult)TypedResults.Ok(results);
    })
    .WithName("Query");

app.MapPut("/{container}", async (
        IHttpClientFactory httpClientFactory,
        [FromRoute] string container,
        [FromBody] JsonObject jsonObject) =>
    {
        var partitionKeyPath = containers[container];
        var partitionKeyValue = jsonObject[partitionKeyPath]!.GetValue<string>();
        await StartEngineContainerAsync(dockerClient, app.Logger, partitionKeyValue);
        
        var body = JsonSerializer.Serialize(jsonObject);
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await GetClient(httpClientFactory, partitionKeyValue).PutAsync(container, content);
        return TypedResults.Ok(response.ReasonPhrase);
    })
    .WithName("Upsert");

app.MapGet("/{container}/{documentId}", async (
        IHttpClientFactory httpClientFactory,
        [FromRoute] string container,
        [FromRoute] string documentId,
        [FromQuery] string partitionKeyValue) =>
    {
        await StartEngineContainerAsync(dockerClient, app.Logger, partitionKeyValue);
        
        var results = await GetClient(httpClientFactory, partitionKeyValue).GetFromJsonAsync<JsonObject>($"{container}/{documentId}");
        return TypedResults.Ok(results);
    })
    .WithName("Get");

app.Run();

HttpClient GetClient(IHttpClientFactory httpClientFactory, string partitionKey)
{
     var httpClient = httpClientFactory.CreateClient();
     var containerName = GetEngineContainerName(partitionKey);
     httpClient.BaseAddress = new Uri($"http://{containerName}:8080");
     return httpClient;
}

int GetHash(string input)
{
    var bytes = Encoding.UTF8.GetBytes(input);
    using var sha = SHA256.Create();
    var hash = sha.ComputeHash(bytes);
    return BitConverter.ToInt32(hash) & 0x7FFFFFFF;
}

async Task StartEngineContainerAsync(DockerClient client, ILogger logger, string partitionKeyValue)
{
    var partitionKeyValueHash = GetHash(partitionKeyValue);
    var containerName = GetEngineContainerName(partitionKeyValue);

    var allContainer = await client.Containers.ListContainersAsync(new ContainersListParameters());
    if (allContainer.Any(container => container.Names[0] == $"/{containerName}"))
    {
        return;
    }

    var volumeName = $"ddsql_engine_{partitionKeyValueHash}_data";
    var volumeResponse = await client.Volumes.CreateAsync(new VolumesCreateParameters
    {
        Name = volumeName
    });
    
    logger.LogInformation($"Volume {partitionKeyValueHash} created at {volumeResponse.Mountpoint}");

    var containerResponse = await client.Containers.CreateContainerAsync(new CreateContainerParameters
    {
        Image = "ddsql_engine",
        Name = containerName,
        HostConfig = new HostConfig
        {
            AutoRemove = true,
            Binds = new List<string>
            {
                $"{volumeName}:/etc/data"
            },
        },
        NetworkingConfig = new NetworkingConfig
        {
            EndpointsConfig = new Dictionary<string, EndpointSettings>
            {
                {
                    "ddsql_network", new EndpointSettings()
                }
            }
        },
        Volumes = new Dictionary<string, EmptyStruct>
        {
            { volumeName, new EmptyStruct() }
        },
        User = "root"
    });

    logger.LogInformation($"Container {partitionKeyValueHash} created: {containerResponse.ID}");
    
    await client.Containers.StartContainerAsync(containerResponse.ID, null);
    logger.LogInformation($"Container {partitionKeyValueHash} started");

    await Task.Delay(2000);
}

string GetEngineContainerName(string partitionKeyValue) => $"ddsql_engine_{GetHash(partitionKeyValue)}";

[Serializable]
public record SqlRequest(string Sql);