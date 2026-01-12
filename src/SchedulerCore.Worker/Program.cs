using Serilog;
using SchedulerCore.Worker.Handlers;
using SchedulerCore.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

// Configure HttpClient for coordinator communication
var coordinatorUrl = builder.Configuration.GetValue<string>("Coordinator:Url") ?? "http://localhost:5000";
builder.Services.AddHttpClient("Coordinator", client =>
{
    client.BaseAddress = new Uri(coordinatorUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register job handlers
builder.Services.AddSingleton<IJobHandler, SampleJobHandler>();
builder.Services.AddSingleton<IJobHandler, EchoJobHandler>();

// Register worker service
builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();

Log.Information("Worker starting, connecting to coordinator at {CoordinatorUrl}", coordinatorUrl);

host.Run();
