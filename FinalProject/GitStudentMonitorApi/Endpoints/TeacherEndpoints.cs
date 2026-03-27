using GitStudentMonitorApi.Models;
using Microsoft.EntityFrameworkCore;

namespace GitStudentMonitorApi.Endpoints;

public static class TeacherEndpoints
{
    public static void MapTeacherEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/teachers").WithTags("Teachers");

        // GET: List all teachers
        group.MapGet("/", async (GitDbContext db) =>
        {
            var teachers = await db.Teachers
                .Select(t => new { t.Id, t.Username, t.Name, t.Email, t.LastLogin })
                .ToListAsync();
            return Results.Ok(teachers);
        })
        .WithName("GetAllTeachers");

        // GET: Get teacher by ID
        group.MapGet("/{id}", async (int id, GitDbContext db) =>
        {
            var teacher = await db.Teachers.FindAsync(id);
            if (teacher == null) return Results.NotFound("Teacher not found.");
            return Results.Ok(new { teacher.Id, teacher.Username, teacher.Name, teacher.Email, teacher.LastLogin });
        })
        .WithName("GetTeacherById");

        // POST: Create teacher
        group.MapPost("/", async (TeacherRequest req, GitDbContext db) =>
        {
            if (await db.Teachers.AnyAsync(t => t.Username == req.Username))
                return Results.Conflict("Username already exists.");

            var teacher = new Teacher
            {
                Username = req.Username,
                Name = req.Name,
                Email = req.Email,
                LastLogin = DateTime.UtcNow
            };

            db.Teachers.Add(teacher);
            await db.SaveChangesAsync();

            return Results.Created($"/api/teachers/{teacher.Id}", new { Message = "Teacher created successfully", TeacherId = teacher.Id });
        })
        .WithName("CreateTeacher");

        // PUT: Update teacher
        group.MapPut("/{id}", async (int id, TeacherUpdateRequest req, GitDbContext db) =>
        {
            var teacher = await db.Teachers.FindAsync(id);
            if (teacher == null) return Results.NotFound("Teacher not found.");

            if (req.Name != null) teacher.Name = req.Name;
            if (req.Email != null) teacher.Email = req.Email;

            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Teacher updated.", TeacherId = teacher.Id });
        })
        .WithName("UpdateTeacher");

        // DELETE: Delete teacher
        group.MapDelete("/{id}", async (int id, GitDbContext db) =>
        {
            var teacher = await db.Teachers.FindAsync(id);
            if (teacher == null) return Results.NotFound("Teacher not found.");

            db.Teachers.Remove(teacher);
            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Teacher deleted." });
        })
        .WithName("DeleteTeacher");
    }
}
