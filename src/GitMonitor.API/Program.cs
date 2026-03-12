using GitMonitor.API.Endpoints;
using GitMonitor.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Register Application Services ──
builder.Services.AddScoped<GitMonitor.Application.Services.ISyncService, GitMonitor.Application.Services.SyncService>();

// ── Register Infrastructure (DbContext, Repositories, Refit, etc.) ──
builder.Services.AddInfrastructure(builder.Configuration);

// ── Swagger ──
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Middleware ──
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "GitMonitor API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

// ── Map Endpoints ──
app.MapTeacherEndpoints();
app.MapClassRoomEndpoints();
app.MapStudentEndpoints();
app.MapSyncEndpoints();

// ── Database Initialization ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GitMonitor.Infrastructure.Data.GitMonitorDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
