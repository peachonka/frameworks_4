using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Practice4.HealthChecks;
using Practice4.Middleware;
using Practice4.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<ProcessStateMachine>();
builder.Services.AddSingleton<ProcessService>();

builder.Services.AddHealthChecks()
    .AddCheck<LivenessCheck>("live")
    .AddCheck<ReadinessCheck>("ready");

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Name == "live"
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Name == "ready"
});

app.Run();