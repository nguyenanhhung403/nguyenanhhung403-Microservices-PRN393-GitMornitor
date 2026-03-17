using Identity.API.DTOs;
using Identity.API.Entities;
using Identity.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Endpoints;

public static class TeacherEndpoints
{
    public static void MapTeacherEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/teachers").WithTags("Teachers");

        group.MapGet("/", async (IdentityDbContext db) =>
        {
            var teachers = await db.Teachers.ToListAsync();
            return Results.Ok(teachers.Select(t => new TeacherResponseDto(t.Id, t.Username, t.Name, t.Email, t.LastLogin)));
        }).WithName("GetAllTeachers");

        group.MapGet("/{id}", async (int id, IdentityDbContext db) =>
        {
            var t = await db.Teachers.FindAsync(id);
            return t != null ? Results.Ok(new TeacherResponseDto(t.Id, t.Username, t.Name, t.Email, t.LastLogin)) : Results.NotFound();
        }).WithName("GetTeacherById");

        group.MapPost("/", async (CreateTeacherDto dto, IdentityDbContext db) =>
        {
            if (await db.Teachers.AnyAsync(t => t.Username == dto.Username))
                return Results.Conflict("Username already exists.");

            var teacher = new Teacher { Username = dto.Username, Name = dto.Name, Email = dto.Email, LastLogin = DateTime.UtcNow };
            db.Teachers.Add(teacher);
            await db.SaveChangesAsync();
            return Results.Created($"/api/teachers/{teacher.Id}", new { teacher.Id, Message = "Teacher created." });
        }).WithName("CreateTeacher");

        group.MapPut("/{id}", async (int id, UpdateTeacherDto dto, IdentityDbContext db) =>
        {
            var teacher = await db.Teachers.FindAsync(id);
            if (teacher == null) return Results.NotFound();
            if (dto.Name != null) teacher.Name = dto.Name;
            if (dto.Email != null) teacher.Email = dto.Email;
            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Teacher updated." });
        }).WithName("UpdateTeacher");

        group.MapDelete("/{id}", async (int id, IdentityDbContext db) =>
        {
            var teacher = await db.Teachers.FindAsync(id);
            if (teacher == null) return Results.NotFound();
            db.Teachers.Remove(teacher);
            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Teacher deleted." });
        }).WithName("DeleteTeacher");
    }
}
