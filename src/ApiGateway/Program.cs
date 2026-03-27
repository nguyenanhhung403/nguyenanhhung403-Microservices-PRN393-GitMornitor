using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment() || true) // Enable Swagger on any environment for demo
{
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/identity/v1/swagger.json", "Identity API");
        c.SwaggerEndpoint("/swagger/classroom/v1/swagger.json", "Classroom API");
        c.SwaggerEndpoint("/swagger/monitoring/v1/swagger.json", "Monitoring API");
        c.RoutePrefix = string.Empty; // serves Swagger UI at root
    });
}

// Configure the HTTP request pipeline.
app.MapReverseProxy();

app.Run();
