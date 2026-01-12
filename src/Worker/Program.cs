using Worker.Handlers;
using Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WorkerOptions>>().Value);

builder.Services.AddHttpClient<WorkerClient>((sp, client) =>
{
    var options = sp.GetRequiredService<WorkerOptions>();
    client.BaseAddress = new Uri(options.CoordinatorUrl);
});

builder.Services.AddHttpClient<HttpGetJobHandler>();
builder.Services.AddTransient<IJobHandler, HttpGetJobHandler>();
builder.Services.AddSingleton<IJobHandler, CpuJobHandler>();
builder.Services.AddSingleton<IJobHandler, FileWriteJobHandler>();

builder.Services.AddSingleton<JobHandlerRegistry>();

builder.Services.AddHostedService<WorkerService>();

var host = builder.Build();
await host.RunAsync();
