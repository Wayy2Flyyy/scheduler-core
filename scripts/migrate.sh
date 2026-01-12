#!/usr/bin/env bash
set -euo pipefail

export RunMigrationsOnly=true

dotnet run --project src/Coordinator
