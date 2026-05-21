$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $projectRoot

Write-Host "Starting PostgreSQL container..." -ForegroundColor Cyan
docker compose up postgres -d

Write-Host ""
Write-Host "PostgreSQL is starting on localhost:5432" -ForegroundColor Green
Write-Host "DB:   shuttle_db"
Write-Host "User: shuttle_user"
Write-Host "Pass: shuttle_pass_dev"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Run migrations: dotnet ef database update --project src/ShuttleApi.Infrastructure --startup-project src/ShuttleApi.Api"
Write-Host "  2. Run API:        dotnet run --project src/ShuttleApi.Api"
