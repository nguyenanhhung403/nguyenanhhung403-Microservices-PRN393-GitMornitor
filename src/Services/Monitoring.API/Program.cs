using Monitoring.API.Data;
using Monitoring.API.Endpoints;
using Monitoring.API.Services;
using Monitoring.API.Services.GitHub;
using Microsoft.EntityFrameworkCore;
using Refit;

var builder = WebApplication.CreateBuilder(args);

// ── DbContext ──
builder.Services.AddDbContext<MonitoringDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── GitHub API & Services ──
builder.Services.AddScoped<GitHubTokenProvider>();
builder.Services.AddTransient<GitHubAuthHandler>();

builder.Services.AddRefitClient<IGitHubApi>()
    .ConfigureHttpClient(c =>
    {
        c.BaseAddress = new Uri("https://api.github.com");
        c.DefaultRequestHeaders.Add("User-Agent", "Monitoring-API");
        c.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        c.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<GitHubAuthHandler>();

builder.Services.AddScoped<IGitHubApiService, GitHubApiService>();
builder.Services.AddScoped<SyncService>();

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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Monitoring API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

// ── Endpoints ──
app.MapSyncEndpoints();

// ── DB Init ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();
    db.Database.EnsureCreated();

    // Manual patch for SQLite schema update (EnsureCreated doesn't handle migrations)
    try {
        db.Database.ExecuteSqlRaw("ALTER TABLE StudentGroups ADD COLUMN LastSyncPushedAt TEXT NULL;");
    } catch { /* Column already exists or table not ready */ }
}

app.Run();
