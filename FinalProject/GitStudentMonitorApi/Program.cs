using GitStudentMonitorApi.Endpoints;
using GitStudentMonitorApi.Models;
using GitStudentMonitorApi.Services;
using Microsoft.EntityFrameworkCore;
using Refit;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──
builder.Services.AddDbContext<GitDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Caching ──
builder.Services.AddMemoryCache();

// ── GitHub API (Refit) ──
builder.Services.AddScoped<GitHubTokenProvider>();
builder.Services.AddTransient<GitHubAuthHandler>();

builder.Services.AddRefitClient<IGitHubApi>()
    .ConfigureHttpClient(c =>
    {
        c.BaseAddress = new Uri("https://api.github.com");
        c.DefaultRequestHeaders.Add("User-Agent", "GitStudentMonitorApi");
        c.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    })
    .AddHttpMessageHandler<GitHubAuthHandler>();

// ── Swagger ──
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Background Worker ──
builder.Services.AddHostedService<GitHubAutoSyncWorker>();

var app = builder.Build();

// ── Middleware Pipeline ──
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "GitStudentMonitorApi v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

// ── Map all endpoint groups ──
app.MapTeacherEndpoints();
app.MapClassroomEndpoints();
app.MapStudentEndpoints();
app.MapSyncEndpoints();

app.Run();
