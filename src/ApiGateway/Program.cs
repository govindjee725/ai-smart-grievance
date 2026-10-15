
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection("ReverseProxy")
    );

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "AI Smart Grievance API Gateway",
    status = "Running"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy"
}));

app.MapReverseProxy();

app.Run();
