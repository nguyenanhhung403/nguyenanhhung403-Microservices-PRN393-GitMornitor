using GitStudentMonitorApi.Models;
using Microsoft.EntityFrameworkCore;

namespace GitStudentMonitorApi.Endpoints;

public static class StudentEndpoints
{
    public static void MapStudentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/students").WithTags("Students");

        // GET: List students (optionally filtered by classRoomId)
        group.MapGet("/", async (int? classRoomId, GitDbContext db) =>
        {
            var query = db.Students
                .Include(s => s.Group)
                .AsQueryable();

            if (classRoomId.HasValue)
            {
                query = query.Where(s => s.Group != null && s.Group.ClassRoomId == classRoomId.Value);
            }

            var students = await query
                .Select(s => new
                {
                    s.Id,
                    s.StudentCode,
                    s.Name,
                    s.GitHubUsername,
                    s.AvatarUrl,
                    GroupName = s.Group != null ? s.Group.GroupName : null,
                    GroupId = s.GroupId
                })
                .ToListAsync();

            return Results.Ok(students);
        })
        .WithName("GetAllStudents");

        // GET: Get student by ID
        group.MapGet("/{id}", async (int id, GitDbContext db) =>
        {
            var student = await db.Students
                .Include(s => s.Group)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null) return Results.NotFound("Student not found.");

            return Results.Ok(new
            {
                student.Id,
                student.StudentCode,
                student.Name,
                student.GitHubUsername,
                student.AvatarUrl,
                GroupName = student.Group?.GroupName,
                GroupId = student.GroupId
            });
        })
        .WithName("GetStudentById");

        // PUT: Update student
        group.MapPut("/{id}", async (int id, StudentUpdateRequest req, GitDbContext db) =>
        {
            var student = await db.Students.FindAsync(id);
            if (student == null) return Results.NotFound("Student not found.");

            if (req.Name != null) student.Name = req.Name;
            if (req.GitHubUsername != null) student.GitHubUsername = req.GitHubUsername;

            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Student updated.", StudentId = student.Id });
        })
        .WithName("UpdateStudent");

        // DELETE: Delete student
        group.MapDelete("/{id}", async (int id, GitDbContext db) =>
        {
            var student = await db.Students.FindAsync(id);
            if (student == null) return Results.NotFound("Student not found.");

            // Also delete sync histories for this student
            var syncHistories = await db.SyncHistories.Where(sh => sh.StudentId == id).ToListAsync();
            db.SyncHistories.RemoveRange(syncHistories);

            db.Students.Remove(student);
            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Student deleted." });
        })
        .WithName("DeleteStudent");
    }
}
