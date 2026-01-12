SHELL := /bin/bash

.PHONY: restore build test format migrate up down

restore:
	dotnet restore

build:
	dotnet build

test:
	dotnet test

format:
	dotnet format

migrate:
	./scripts/migrate.sh

up:
	docker compose up --build

down:
	docker compose down -v
