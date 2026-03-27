pushd "e:\.Net\FP2"
Remove-Item "*.sln" -ErrorAction SilentlyContinue
dotnet new sln --name GitMonitor -f sln --force
dotnet sln GitMonitor.sln add src\Services\Identity.API\Identity.API.csproj
dotnet sln GitMonitor.sln add src\Services\Classroom.API\Classroom.API.csproj
dotnet sln GitMonitor.sln add src\Services\Monitoring.API\Monitoring.API.csproj
popd
