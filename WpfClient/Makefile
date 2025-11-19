SOLUTION_DIR := .
PROJECT := WpfClient.csproj

.PHONY: build run clean docker-up docker-down docker-logs

## Build WPF client
build:
	dotnet build $(PROJECT)

## Run WPF client
run:
	dotnet run --project $(PROJECT)

## Clean build artifacts
clean:
	dotnet clean $(PROJECT)

## Start MySQL database via docker-compose
docker-up:
	docker-compose up -d

## Stop MySQL database and remove containers
docker-down:
	docker-compose down

## Show MySQL container logs
docker-logs:
	docker logs -f paint_mysql



