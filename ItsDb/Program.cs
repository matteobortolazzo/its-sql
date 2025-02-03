using ItsDb.Interpreter;
using Microsoft.AspNetCore.Mvc;

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

app.MapPost("/query", ([FromBody] SqlRequest request) =>
    {
        var lexer = new Lexer();
        var tokens = lexer.Tokenize(request.Sql);
        var parser = new Parser();
        var ast = parser.Parse(tokens);
        var runner = new Runner();
        var result = runner.Run(ast);
        return TypedResults.Ok(result);
    })
    .WithName("Query");

app.Run();

[Serializable]
public record SqlRequest(string Sql);