using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gateway.Interpreter;
using Microsoft.AspNetCore.Mvc;

const string engineAddress = "http://db_engine:8080";

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("gateway", opt =>
{
    opt.BaseAddress = new Uri(engineAddress);
});

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

app.MapPost("/{container}/query", async (
        IHttpClientFactory httpClientFactory, 
        [FromRoute] string container, [FromBody] SqlRequest request) =>
    {
        var lexer = new Lexer();
        var tokens = lexer.Tokenize(request.Sql);
        var parser = new Parser();
        var ast = parser.Parse(tokens);

        var httpClient = httpClientFactory.CreateClient("gateway");
        var response = await httpClient.PostAsJsonAsync($"{container}/query", ast);
        var results = await response.Content.ReadFromJsonAsync<JsonObject[]>();
        return TypedResults.Ok(results);
    })
    .WithName("Query");

app.MapPut("/{container}", async (
        ILogger<Program> logger,
        IHttpClientFactory httpClientFactory,
        [FromRoute] string container,
        [FromBody] JsonObject jsonObject) =>
    {
        var partitionKeyPath = containers[container];
        var partitionKeyValue = jsonObject[partitionKeyPath];
        
        logger.LogInformation(partitionKeyValue.ToString());
        
        var httpClient = httpClientFactory.CreateClient("gateway");

        var body = JsonSerializer.Serialize(jsonObject);
        
        logger.LogInformation(body);
        
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await httpClient.PutAsync(container, content);
        return TypedResults.Ok(response.ReasonPhrase);
    })
    .WithName("Upsert");



app.MapGet("/{container}/{documentId}", async (
        IHttpClientFactory httpClientFactory,
        [FromRoute] string container,
        [FromRoute] string documentId) =>
    {
        var httpClient = httpClientFactory.CreateClient("gateway");
        var results = await httpClient.GetFromJsonAsync<JsonObject>($"{container}/{documentId}");
        return TypedResults.Ok(results);
    })
    .WithName("Get");

app.Run();

[Serializable]
public record SqlRequest(string Sql);