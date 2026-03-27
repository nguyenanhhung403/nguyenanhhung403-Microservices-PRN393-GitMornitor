Write-Host "Starting GitMonitor Microservices..."

Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd `"$PSScriptRoot\src\Services\Identity.API`"; dotnet run" -WindowStyle Normal
Write-Host "Identity.API started on http://localhost:5051"

Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd `"$PSScriptRoot\src\Services\Classroom.API`"; dotnet run" -WindowStyle Normal
Write-Host "Classroom.API started on http://localhost:5020"

Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd `"$PSScriptRoot\src\Services\Monitoring.API`"; dotnet run" -WindowStyle Normal
Write-Host "Monitoring.API started on http://localhost:5039"

Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd `"$PSScriptRoot\src\ApiGateway`"; dotnet run" -WindowStyle Normal
Write-Host "ApiGateway started on http://localhost:5000"

Write-Host "All services are starting up in separate windows!"
