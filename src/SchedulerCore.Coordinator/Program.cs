using Microsoft.EntityFrameworkCore;
using Serilog;
using SchedulerCore.Coordinator.Services;
using SchedulerCore.Domain.Interfaces;
using SchedulerCore.Persistence;
using SchedulerCore.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SchedulerDbContext>();

// Configure database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=scheduler.db";
builder.Services.AddDbContext<SchedulerDbContext>(options =>
    options.UseSqlite(connectionString));

// Register repositories
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IWorkerRepository, WorkerRepository>();

// Register background services
builder.Services.AddHostedService<LeaseMonitorService>();
builder.Services.AddHostedService<WorkerHealthMonitorService>();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    Log.Information("Database initialized");
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

var urls = builder.Configuration.GetValue<string>("Urls") ?? "http://localhost:5000";
Log.Information("Scheduler Coordinator starting on {Urls}", urls);

app.Run();
