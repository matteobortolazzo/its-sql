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

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection();

var containers = new Dictionary<string, string>()
{
    { "users", "city" }
};

var dockerClient = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();

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
        
        var httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri($"http://engine_{GetHash(partitionKeyValue)}:8080");
        var response = await httpClient.PostAsJsonAsync($"{container}/query", ast);
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
        
        var httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri($"http://engine_{GetHash(partitionKeyValue)}:8080");
        var body = JsonSerializer.Serialize(jsonObject);
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await httpClient.PutAsync(container, content);
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
        
        var httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri($"http://engine_{GetHash(partitionKeyValue)}:8080");
        var results = await httpClient.GetFromJsonAsync<JsonObject>($"{container}/{documentId}");
        return TypedResults.Ok(results);
    })
    .WithName("Get");

app.Run();

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
    var containerName = $"engine_{partitionKeyValueHash}";

    var allContainer = await client.Containers.ListContainersAsync(new ContainersListParameters());
    if (allContainer.Any(container => container.Names[0] == $"/{containerName}"))
    {
        return;
    }

    var volumeName = $"volume_{partitionKeyValueHash}";
    var volumeResponse = await client.Volumes.CreateAsync(new VolumesCreateParameters
    {
        Name = volumeName
    });
    
    logger.LogInformation($"Volume {partitionKeyValueHash} created at {volumeResponse.Mountpoint}");

    var containerResponse = await client.Containers.CreateContainerAsync(new CreateContainerParameters
    {
        Image = "db_engine_image",
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
                    "src_db_network", new EndpointSettings()
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

[Serializable]
public record SqlRequest(string Sql);