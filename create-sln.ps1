Set-Location -Path "e:\.Net\FP2"
dotnet new sln -n GitMonitor --force
dotnet sln GitMonitor.sln add src\Services\Identity.API\Identity.API.csproj
dotnet sln GitMonitor.sln add src\Services\Classroom.API\Classroom.API.csproj
dotnet sln GitMonitor.sln add src\Services\Monitoring.API\Monitoring.API.csproj
