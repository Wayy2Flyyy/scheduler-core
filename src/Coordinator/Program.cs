using System.Text.Json;
using Coordinator.Data;
using Coordinator.HostedServices;
using Coordinator.Metrics;
using Coordinator.Services;
using Coordinator.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddDbContext<SchedulerDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("SchedulerDb"));
    options.UseSnakeCaseNamingConvention();
});

builder.Services.AddSingleton<MetricsRegistry>();
builder.Services.Configure<CoordinatorOptions>(builder.Configuration.GetSection("Coordinator"));
builder.Services.AddScoped(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CoordinatorOptions>>().Value);

builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<DatabaseMigrator>();

builder.Services.AddHostedService<LeaseMonitorService>();
builder.Services.AddHostedService<RecurringSchedulerService>();

builder.Services.AddValidatorsFromAssemblyContaining<JobSubmissionRequestValidator>();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    await migrator.ApplyMigrationsAsync(CancellationToken.None);
}

if (builder.Configuration.GetValue<bool>("RunMigrationsOnly"))
{
    return;
}

app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/metrics", (MetricsRegistry metrics) =>
{
    var snapshot = metrics.Snapshot();
    var lines = snapshot.Select(kvp => $"scheduler_{kvp.Key} {kvp.Value}");
    return Results.Text(string.Join('\n', lines), "text/plain");
});

app.MapPost("/api/jobs", async (
    JobSubmissionRequest request,
    IValidator<JobSubmissionRequest> validator,
    JobService jobService,
    CancellationToken cancellationToken) =>
{
    var validation = await validator.ValidateAsync(request, cancellationToken);
    if (!validation.IsValid)
    {
        return Results.ValidationProblem(validation.ToDictionary());
    }

    var response = await jobService.SubmitJobAsync(request, cancellationToken);
    return Results.Ok(response);
});

app.MapGet("/api/jobs", async (
    [FromQuery] JobStatus? status,
    [FromQuery] int skip,
    [FromQuery] int take,
    JobService jobService,
    CancellationToken cancellationToken) =>
{
    var safeTake = take <= 0 ? 25 : Math.Min(take, 200);
    var response = await jobService.ListJobsAsync(status, Math.Max(0, skip), safeTake, cancellationToken);
    return Results.Ok(response);
});

app.MapGet("/api/jobs/{jobId:guid}", async (Guid jobId, JobService jobService, CancellationToken cancellationToken) =>
{
    var job = await jobService.GetJobAsync(jobId, cancellationToken);
    return job is null ? Results.NotFound() : Results.Ok(job);
});

app.MapGet("/api/jobs/{jobId:guid}/runs", async (Guid jobId, JobService jobService, CancellationToken cancellationToken) =>
{
    var runs = await jobService.GetJobRunsAsync(jobId, cancellationToken);
    return Results.Ok(runs);
});

app.MapPost("/api/jobs/{jobId:guid}/cancel", async (Guid jobId, JobService jobService, CancellationToken cancellationToken) =>
{
    var success = await jobService.CancelJobAsync(jobId, cancellationToken);
    return success ? Results.Ok(new { jobId }) : Results.NotFound();
});

app.MapPost("/api/jobs/{jobId:guid}/retry", async (
    Guid jobId,
    RetryJobRequest request,
    JobService jobService,
    CancellationToken cancellationToken) =>
{
    var success = await jobService.RetryJobAsync(jobId, request.Reason, cancellationToken);
    return success ? Results.Ok(new { jobId }) : Results.NotFound();
});

app.MapPost("/api/workers/claim", async (
    ClaimJobsRequest request,
    JobService jobService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.WorkerId) || request.MaxJobs <= 0 || request.LeaseSeconds <= 0)
    {
        return Results.BadRequest(new { error = "Invalid claim request." });
    }

    var response = await jobService.ClaimJobsAsync(request, cancellationToken);
    return Results.Ok(response);
});

app.MapPost("/api/workers/complete", async (
    JobCompletionRequest request,
    JobService jobService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.WorkerId))
    {
        return Results.BadRequest(new { error = "WorkerId is required." });
    }

    var success = await jobService.CompleteJobAsync(request, cancellationToken);
    return success ? Results.Ok(new { jobId = request.JobId }) : Results.NotFound();
});

app.MapPost("/api/workers/renew", async (
    RenewLeaseRequest request,
    JobService jobService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.WorkerId))
    {
        return Results.BadRequest(new { error = "WorkerId is required." });
    }

    var response = await jobService.RenewLeaseAsync(request, cancellationToken);
    return Results.Ok(response);
});

app.MapPost("/api/workers/heartbeat", async (
    WorkerHeartbeatRequest request,
    JobService jobService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.WorkerId))
    {
        return Results.BadRequest(new { error = "WorkerId is required." });
    }

    await jobService.RecordHeartbeatAsync(request, cancellationToken);
    return Results.Ok(new { status = "ok" });
});

app.MapPost("/api/seed", async (JobService jobService, CancellationToken cancellationToken) =>
{
    var sample = new JobSubmissionRequest(
        JobTypes.HttpGet,
        JsonSerializer.Deserialize<JsonElement>("{\"url\":\"https://example.com\"}"),
        DateTimeOffset.UtcNow.AddSeconds(5),
        null,
        3,
        30,
        "seed-http");

    var response = await jobService.SubmitJobAsync(sample, cancellationToken);
    return Results.Ok(response);
});

app.MapFallbackToFile("index.html");

app.Run();
