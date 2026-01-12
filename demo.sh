#!/bin/bash

# Example script demonstrating the scheduler system
# This script shows how to interact with the scheduler API

COORDINATOR_URL="http://localhost:5000"

echo "===== Scheduler Core Demo ====="
echo ""

# Create a sample job
echo "1. Creating a sample job..."
JOB_RESPONSE=$(curl -s -X POST "$COORDINATOR_URL/api/jobs" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Demo Sample Job",
    "type": "sample",
    "payload": "This is a demo job that takes 5 seconds",
    "maxRetries": 3,
    "priority": 10
  }')

JOB_ID=$(echo $JOB_RESPONSE | grep -o '"id":"[^"]*"' | cut -d'"' -f4)
echo "Created job with ID: $JOB_ID"
echo ""

# Create an echo job
echo "2. Creating an echo job..."
ECHO_RESPONSE=$(curl -s -X POST "$COORDINATOR_URL/api/jobs" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Echo Job",
    "type": "echo",
    "payload": "Echo: Hello from scheduler-core!",
    "maxRetries": 2,
    "priority": 5
  }')

ECHO_ID=$(echo $ECHO_RESPONSE | grep -o '"id":"[^"]*"' | cut -d'"' -f4)
echo "Created echo job with ID: $ECHO_ID"
echo ""

# List all pending jobs
echo "3. Listing all pending jobs..."
curl -s "$COORDINATOR_URL/api/jobs?status=pending" | json_pp 2>/dev/null || curl -s "$COORDINATOR_URL/api/jobs?status=pending"
echo ""

# Check health
echo "4. Checking coordinator health..."
HEALTH=$(curl -s "$COORDINATOR_URL/health")
echo "Health status: $HEALTH"
echo ""

# List workers
echo "5. Listing active workers..."
curl -s "$COORDINATOR_URL/api/workers" | json_pp 2>/dev/null || curl -s "$COORDINATOR_URL/api/workers"
echo ""

echo "===== Demo Complete ====="
echo "To view Swagger UI, visit: $COORDINATOR_URL/swagger"
echo "To check a specific job, use: curl $COORDINATOR_URL/api/jobs/{job-id}"
