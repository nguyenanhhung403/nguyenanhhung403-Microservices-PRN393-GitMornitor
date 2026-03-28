using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Reverse Proxy ──
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ── CORS ──
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b =>
    {
        b.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader();
    });
});

// ── JWT Authentication ──
var jwtConfig = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig["Issuer"],
            ValidAudience = jwtConfig["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtConfig["Key"]!))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment() || true)
{
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/identity/v1/swagger.json", "Identity API");
        c.SwaggerEndpoint("/swagger/classroom/v1/swagger.json", "Classroom API");
        c.SwaggerEndpoint("/swagger/monitoring/v1/swagger.json", "Monitoring API");
        c.RoutePrefix = string.Empty;
    });
}

app.UseAuthentication();
app.UseAuthorization();

// ── Auth middleware: validate JWT and forward TeacherId header ──
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    // Skip auth for login/register and swagger
    if (path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
        path == "/")
    {
        await next();
        return;
    }

    // Require authentication for all /api/* routes
    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { Message = "Unauthorized. Please login first." });
            return;
        }

        // Forward TeacherId claim as header to downstream services
        var teacherId = context.User.FindFirst("TeacherId")?.Value;
        if (teacherId != null)
        {
            context.Request.Headers["X-Teacher-Id"] = teacherId;
        }
    }

    await next();
});

app.MapReverseProxy();

app.Run();
