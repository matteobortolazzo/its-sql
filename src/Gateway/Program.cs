using System.Text.Json;
using System.Text.Json.Nodes;
using Gateway.Interpreter;
using Microsoft.AspNetCore.Mvc;

const string engineAddress = "http://localhost:5263";

var builder = WebApplication.CreateBuilder(args);

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
        return TypedResults.Ok(response.IsSuccessStatusCode);
    })
    .WithName("Query");

app.MapPut("/{container}", ([FromRoute] string container, [FromBody] JsonObject jsonObject) =>
    {
        var partitionKeyPath = containers[container];
        var partitionKeyValue = jsonObject[partitionKeyPath];
        // Decide which node to call
        return TypedResults.Ok(partitionKeyValue);
    })
    .WithName("Upsert");

app.Run();

[Serializable]
public record SqlRequest(string Sql);