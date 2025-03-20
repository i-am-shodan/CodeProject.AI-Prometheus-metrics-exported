using Log2Metric;
using Prometheus;

Metrics.SuppressDefaultMetrics();

var builder = WebApplication.CreateBuilder(args);

var containerName = Environment.GetEnvironmentVariable("CONTAINER") ?? throw new Exception("Invalid CONTAINER variable");

// Add services to the container.
DockerContainerLogStream logStream = new(containerName);

var app = builder.Build();
app.UseMetricServer();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.Run();
