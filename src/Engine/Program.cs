using System.Text.Json;
using System.Text.Json.Nodes;
using Engine;
using Microsoft.AspNetCore.Mvc;
using Shared;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/{container}/query", async (
        ILogger<Program> logger,
        [FromRoute] string container, [FromBody] Node body) =>
    {
        var runner = new Runner(logger, container);
        var result = await runner.RunAsync(body);
        return TypedResults.Ok(result);
    })
    .WithName("Query");

app.MapPut("{container}/", async (
        ILogger<Program> logger,
        [FromRoute] string container, 
        [FromBody] JsonObject body) =>
    {
        var containerPath = GetContainerPath(container);
        if (!Directory.Exists(containerPath))
        {
            Directory.CreateDirectory(containerPath);
        }
        
        var id = body["id"]!.ToString();
        
        logger.LogInformation(id);
        
        var json = JsonSerializer.Serialize(body);
        await File.WriteAllTextAsync($"{containerPath}/{id}.json", json);
        return TypedResults.Ok();
    })
    .WithName("Upsert");


app.MapGet("{container}/{documentId}", async (
        [FromRoute] string container, 
        [FromRoute] string documentId) => 
    {
        var path = $"{GetContainerPath(container)}/{documentId}.json";
        if (!File.Exists(path))
        {
            return (IResult)TypedResults.NotFound();
        }
        
        var json = await File.ReadAllTextAsync(path);
        var obj = JsonSerializer.Deserialize<JsonObject>(json);
        return TypedResults.Ok(obj);
    })
    .WithName("Read");

app.Run();
return;

string GetContainerPath(string container) => $"/etc/data/{container}";