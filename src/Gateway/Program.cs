using Docker.DotNet;
using Gateway.Features.Containers;
using Gateway.Features.Documents;
using Gateway.Interpreter;
using Gateway.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

var dockerHostUri = new Uri(Environment.GetEnvironmentVariable("DOCKER_HOST")!);
var dockerClient = new DockerClientConfiguration(dockerHostUri).CreateClient();
builder.Services.AddSingleton(dockerClient);

builder.Services.AddSingleton<Lexer>();
builder.Services.AddSingleton<Parser>();

builder.Services.AddSingleton<DockerService>();
builder.Services.AddSingleton<EngineService>();
builder.Services.AddSingleton<PartitionService>();
builder.Services.AddSingleton<QueryService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGroup("containers")
    .MapQueryContainer();

app.MapGroup("containers/{container}/documents")
    .MapUpsertDocument()
    .MapGetDocument();

app.Run();