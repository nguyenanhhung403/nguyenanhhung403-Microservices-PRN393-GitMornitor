using Classroom.API.DTOs;
using Classroom.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Classroom.API.Endpoints;

public static class StudentEndpoints
{
    private static bool TryGetTeacherId(HttpContext httpContext, out int teacherId)
    {
        teacherId = 0;
        return int.TryParse(httpContext.Request.Headers["X-Teacher-Id"].FirstOrDefault(), out teacherId);
    }

    public static void MapStudentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/students").WithTags("Students");

        group.MapGet("/", async (int? classRoomId, HttpContext httpContext, ClassroomDbContext db) =>
        {
            if (!TryGetTeacherId(httpContext, out var teacherId))
                return Results.Unauthorized();

            var query = db.Students.Include(s => s.Group).ThenInclude(g => g.ClassRoom).AsQueryable();

            // Only return students from classrooms owned by this teacher
            query = query.Where(s => s.Group.ClassRoom.TeacherId == teacherId);

            if (classRoomId.HasValue)
                query = query.Where(s => s.Group.ClassRoomId == classRoomId.Value);

            var students = await query.ToListAsync();

            return Results.Ok(students.Select(s => new StudentResponseDto(
                s.Id, s.StudentCode, s.Name, s.GitHubUsername, s.AvatarUrl, s.Email, s.IsLeader,
                s.Group?.GroupName, s.GroupId)));
        }).WithName("GetAllStudents");

        group.MapGet("/{id}", async (int id, HttpContext httpContext, ClassroomDbContext db) =>
        {
            if (!TryGetTeacherId(httpContext, out var teacherId))
                return Results.Unauthorized();

            var s = await db.Students.Include(st => st.Group).ThenInclude(g => g.ClassRoom).FirstOrDefaultAsync(st => st.Id == id);
            if (s == null) return Results.NotFound();
            if (s.Group?.ClassRoom?.TeacherId != teacherId) return Results.Forbid();

            return Results.Ok(new StudentResponseDto(s.Id, s.StudentCode, s.Name, s.GitHubUsername, s.AvatarUrl, s.Email, s.IsLeader, s.Group?.GroupName, s.GroupId));
        }).WithName("GetStudentById");

        group.MapPut("/{id}", async (int id, UpdateStudentDto dto, HttpContext httpContext, ClassroomDbContext db) =>
        {
            if (!TryGetTeacherId(httpContext, out var teacherId))
                return Results.Unauthorized();

            var s = await db.Students.Include(st => st.Group).ThenInclude(g => g.ClassRoom).FirstOrDefaultAsync(st => st.Id == id);
            if (s == null) return Results.NotFound();
            if (s.Group?.ClassRoom?.TeacherId != teacherId) return Results.Forbid();

            if (dto.Name != null) s.Name = dto.Name;
            if (dto.GitHubUsername != null) s.GitHubUsername = dto.GitHubUsername;
            if (dto.Email != null) s.Email = dto.Email;
            if (dto.IsLeader.HasValue) s.IsLeader = dto.IsLeader.Value;

            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Student updated." });
        }).WithName("UpdateStudent");

        group.MapDelete("/{id}", async (int id, HttpContext httpContext, ClassroomDbContext db) =>
        {
            if (!TryGetTeacherId(httpContext, out var teacherId))
                return Results.Unauthorized();

            var s = await db.Students.Include(st => st.Group).ThenInclude(g => g.ClassRoom).FirstOrDefaultAsync(st => st.Id == id);
            if (s == null) return Results.NotFound();
            if (s.Group?.ClassRoom?.TeacherId != teacherId) return Results.Forbid();

            db.Students.Remove(s);
            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Student deleted." });
        }).WithName("DeleteStudent");
    }
}
