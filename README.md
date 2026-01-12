# scheduler-core

**Distributed Job Scheduler built in C# (.NET 8)**  
Coordinator + workers with persistence, retries, leases, and observability.

`scheduler-core` is a production-oriented distributed job scheduling system designed to reliably execute background jobs across multiple worker nodes. It focuses on correctness, fault tolerance, and maintainability over hype.

## Architecture

The system consists of two main components:

### Coordinator
- **REST API** for job submission and monitoring
- **Lease Management** - Assigns jobs to workers with time-limited leases
- **Health Monitoring** - Tracks worker heartbeats and marks dead workers
- **Automatic Recovery** - Releases expired leases and reassigns jobs
- **SQLite Persistence** - Stores jobs and worker state

### Worker
- **Job Polling** - Continuously polls coordinator for available jobs
- **Heartbeat** - Sends periodic heartbeats to coordinator
- **Pluggable Handlers** - Extensible job handler system
- **Lease Renewal** - Maintains job leases during execution
- **Retry Logic** - Automatically retries failed jobs

## Features

- ✅ **Distributed execution** across multiple worker nodes
- ✅ **Job persistence** with SQLite (easily swappable for PostgreSQL/MySQL)
- ✅ **Automatic retries** with configurable retry limits
- ✅ **Job leases** prevent duplicate execution
- ✅ **Dead worker detection** and job reassignment
- ✅ **Priority-based scheduling** for job ordering
- ✅ **Structured logging** with Serilog
- ✅ **Health check endpoints** for monitoring
- ✅ **Docker support** for easy deployment
- ✅ **Comprehensive tests** with xUnit

## Getting Started

### Prerequisites
- .NET 8 SDK
- Docker (optional, for containerized deployment)

### Running Locally

1. **Build the solution**
   ```bash
   dotnet build
   ```

2. **Run tests**
   ```bash
   dotnet test
   ```

3. **Start the Coordinator**
   ```bash
   cd src/SchedulerCore.Coordinator
   dotnet run
   ```
   The coordinator will start on `http://localhost:5000`

4. **Start one or more Workers** (in separate terminals)
   ```bash
   cd src/SchedulerCore.Worker
   dotnet run
   ```

### Using Docker Compose

```bash
docker-compose up --build
```

This starts:
- 1 coordinator on port 5000
- 2 worker instances

## API Usage

### Create a Job

```bash
curl -X POST http://localhost:5000/api/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Sample Job",
    "type": "sample",
    "payload": "Hello, World!",
    "maxRetries": 3,
    "priority": 5
  }'
```

### Get Job Status

```bash
curl http://localhost:5000/api/jobs/{jobId}
```

### List Jobs by Status

```bash
curl http://localhost:5000/api/jobs?status=pending
curl http://localhost:5000/api/jobs?status=running
curl http://localhost:5000/api/jobs?status=completed
```

### List Active Workers

```bash
curl http://localhost:5000/api/workers
```

### Health Check

```bash
curl http://localhost:5000/health
```

## Configuration

### Coordinator (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=scheduler.db"
  },
  "Urls": "http://localhost:5000"
}
```

### Worker (appsettings.json)

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

## Extending with Custom Job Handlers

Create a new job handler by implementing `IJobHandler`:

```csharp
public class MyCustomHandler : IJobHandler
{
    public string JobType => "my-custom-job";

    public async Task<bool> ExecuteAsync(Job job, CancellationToken cancellationToken)
    {
        // Your job logic here
        await Task.Delay(1000);
        return true; // Return true on success, false on failure
    }
}
```

Register it in `Program.cs`:

```csharp
builder.Services.AddSingleton<IJobHandler, MyCustomHandler>();
```

## Project Structure

```
scheduler-core/
├── src/
│   ├── SchedulerCore.Domain/          # Core entities and interfaces
│   ├── SchedulerCore.Persistence/      # Database layer (EF Core)
│   ├── SchedulerCore.Coordinator/      # REST API and monitoring services
│   └── SchedulerCore.Worker/           # Worker node implementation
├── tests/
│   └── SchedulerCore.Tests/            # Unit and integration tests
├── docker-compose.yml                  # Docker orchestration
├── Dockerfile.coordinator              # Coordinator container
└── Dockerfile.worker                   # Worker container
```

## Design Decisions

- **SQLite for Simplicity** - Easy to get started, can be swapped for production databases
- **Lease-based Execution** - Prevents duplicate job execution across workers
- **No Message Queue** - Polling-based for simplicity, can add queue later for higher throughput
- **Structured Logging** - Serilog for production-ready observability
- **Minimal Dependencies** - Only essential packages for core functionality

## Future Enhancements

- [ ] Job scheduling with cron expressions
- [ ] Job dependencies and workflows
- [ ] Web dashboard for monitoring
- [ ] Metrics export (Prometheus)
- [ ] Dead letter queue for failed jobs
- [ ] Job cancellation API
- [ ] Horizontal scaling with distributed locking

## License

MIT