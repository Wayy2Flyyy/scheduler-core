# Getting Started with scheduler-core

This guide will help you get the scheduler system up and running quickly.

## Prerequisites

- .NET 8 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Git
- curl (for testing API endpoints)
- Docker (optional, for containerized deployment)

## Quick Start

### 1. Clone and Build

```bash
# Clone the repository
git clone https://github.com/Wayy2Flyyy/scheduler-core.git
cd scheduler-core

# Build the solution
dotnet build

# Run tests
dotnet test
```

### 2. Start the Coordinator

In one terminal:

```bash
cd src/SchedulerCore.Coordinator
dotnet run
```

You should see:
```
[INFO] Database initialized
[INFO] Scheduler Coordinator starting on http://localhost:5000
[INFO] Lease Monitor Service started
[INFO] Worker Health Monitor Service started
```

### 3. Start One or More Workers

In separate terminals:

```bash
cd src/SchedulerCore.Worker
dotnet run
```

You should see:
```
[INFO] Worker starting, connecting to coordinator at http://localhost:5000
[INFO] Worker {name} ({id}) starting
```

### 4. Submit Your First Job

```bash
curl -X POST http://localhost:5000/api/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My First Job",
    "type": "sample",
    "payload": "Hello, World!",
    "maxRetries": 3,
    "priority": 5
  }'
```

Response:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "My First Job",
  "type": "sample",
  "status": "Pending",
  ...
}
```

## Available Job Types

The system comes with two built-in job handlers:

1. **sample** - Simulates work by sleeping for 5 seconds
2. **echo** - Logs the payload and completes immediately

## API Endpoints

### Jobs

- `POST /api/jobs` - Create a new job
- `GET /api/jobs/{id}` - Get job details
- `GET /api/jobs?status=pending` - List jobs by status

### Workers

- `GET /api/workers` - List active workers
- `GET /api/workers/{id}` - Get worker details

### Monitoring

- `GET /health` - Health check endpoint
- `GET /swagger` - Swagger UI for API documentation

## Configuration

### Coordinator Configuration

Edit `src/SchedulerCore.Coordinator/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=scheduler.db"
  },
  "Urls": "http://localhost:5000"
}
```

### Worker Configuration

Edit `src/SchedulerCore.Worker/appsettings.json`:

```json
{
  "Coordinator": {
    "Url": "http://localhost:5000"
  },
  "Worker": {
    "PollIntervalSeconds": 10,
    "HeartbeatIntervalSeconds": 30,
    "LeaseDurationMinutes": 5
  }
}
```

## Docker Deployment

### Using Docker Compose

```bash
# Build and start all services
docker-compose up --build

# Start in detached mode
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

This will start:
- 1 Coordinator on port 5000
- 2 Worker instances

### Individual Docker Images

Build the Coordinator:
```bash
docker build -f Dockerfile.coordinator -t scheduler-coordinator .
docker run -p 5000:5000 scheduler-coordinator
```

Build the Worker:
```bash
docker build -f Dockerfile.worker -t scheduler-worker .
docker run -e Coordinator__Url=http://host.docker.internal:5000 scheduler-worker
```

## Creating Custom Job Handlers

1. Create a new handler class:

```csharp
public class MyCustomHandler : IJobHandler
{
    private readonly ILogger<MyCustomHandler> _logger;
    
    public string JobType => "my-custom-job";
    
    public MyCustomHandler(ILogger<MyCustomHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task<bool> ExecuteAsync(Job job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing job {JobId} with payload: {Payload}", 
            job.Id, job.Payload);
        
        // Your custom logic here
        await DoWorkAsync(job.Payload, cancellationToken);
        
        return true; // Return true for success, false for failure
    }
    
    private async Task DoWorkAsync(string payload, CancellationToken cancellationToken)
    {
        // Implementation
        await Task.Delay(1000, cancellationToken);
    }
}
```

2. Register it in `src/SchedulerCore.Worker/Program.cs`:

```csharp
builder.Services.AddSingleton<IJobHandler, MyCustomHandler>();
```

3. Submit jobs with your custom type:

```bash
curl -X POST http://localhost:5000/api/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Custom Job",
    "type": "my-custom-job",
    "payload": "custom data"
  }'
```

## Monitoring and Observability

### Logs

All services use structured logging with Serilog. Logs are output to the console with timestamps and log levels.

### Health Checks

The Coordinator exposes a health check endpoint at `/health` that validates database connectivity.

```bash
curl http://localhost:5000/health
# Response: Healthy
```

### Swagger UI

Access the interactive API documentation at:
```
http://localhost:5000/swagger
```

## Troubleshooting

### Worker not picking up jobs

1. Check that the worker is connected to the correct coordinator URL
2. Verify the coordinator is running and accessible
3. Check worker logs for errors
4. Ensure the job type matches a registered handler

### Jobs stuck in "Running" state

Jobs have a lease duration (default 5 minutes). If a worker dies while processing a job, the coordinator will automatically release the lease after expiration and reassign the job.

### Database errors

The system uses SQLite by default. The database file is created automatically at startup. If you encounter issues:

```bash
# Remove the database and restart
rm src/SchedulerCore.Coordinator/scheduler.db
```

## Next Steps

- Read the main [README.md](README.md) for architecture details
- Check the [API documentation](http://localhost:5000/swagger) when the coordinator is running
- Look at the test files in `tests/SchedulerCore.Tests/` for usage examples
- Extend the system with your own job handlers

## Support

For issues and questions, please create an issue on the [GitHub repository](https://github.com/Wayy2Flyyy/scheduler-core).
