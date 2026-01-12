# scheduler-core

**Distributed Job Scheduler built in C# (.NET 8)**  
Coordinator + workers with persistence, retries, leases, and observability.

`scheduler-core` is a production-oriented distributed job scheduling system designed to reliably execute background jobs across multiple worker nodes. It focuses on correctness, fault tolerance, and maintainability over hype.

---

## Features

- Coordinator / worker architecture
- Persistent job state (PostgreSQL)
- At-least-once execution with lease-based claiming
- Automatic retries with exponential backoff
- Delayed and recurring jobs
- Idempotent job submission
- Horizontal worker scaling
- Graceful shutdown handling
- Structured logging and health checks
- Local-first development via Docker Compose

---

## Architecture Overview

**Coordinator**
- ASP.NET Core service
- Accepts job submissions via REST API
- Manages scheduling, retries, leases, and state
- Exposes health, metrics, and job status endpoints
- Optional minimal web dashboard

**Worker**
- .NET hosted service
- Polls coordinator for work
- Executes jobs with configurable concurrency
- Reports heartbeats and execution results
- Handles crashes and safe lease release

**Shared**
- Job contracts and DTOs
- Validation and common utilities
- Shared scheduling and retry logic

---

## Project Structure

