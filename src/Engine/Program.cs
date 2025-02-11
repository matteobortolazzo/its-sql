using Engine.Features;
using Engine.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddSingleton<QueryExecutor>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGroup("{container}")
    .MapGetDocument()
    .MapUpsertDocument()
    .MapQueryContainer();

app.Run();