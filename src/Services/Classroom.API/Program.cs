using Classroom.API.Data;
using Classroom.API.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── DbContext ──
builder.Services.AddDbContext<ClassroomDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Classroom API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

// ── Endpoints ──
app.MapClassRoomEndpoints();
app.MapStudentEndpoints();

// ── DB Init ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClassroomDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
