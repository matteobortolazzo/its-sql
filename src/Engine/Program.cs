using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Shared;

var builder = WebApplication.CreateBuilder(args);

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

app.MapPost("/{container}/query", ([FromRoute] string container, [FromBody] Node body) =>
    {
        return TypedResults.Ok(body);
    })
    .WithName("Query");

app.MapPut("{container}/", ([FromRoute] string container, [FromBody] JsonObject body) =>
    {
        var c = body["city"];
        return TypedResults.Ok();
    })
    .WithName("Upsert");

app.Run();

[Serializable]
public record SqlRequest(string Sql);
