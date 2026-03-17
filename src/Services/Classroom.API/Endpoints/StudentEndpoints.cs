using Classroom.API.DTOs;
using Classroom.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Classroom.API.Endpoints;

public static class StudentEndpoints
{
    public static void MapStudentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/students").WithTags("Students");

        group.MapGet("/", async (int? classRoomId, ClassroomDbContext db) =>
        {
            var query = db.Students.Include(s => s.Group).AsQueryable();

            if (classRoomId.HasValue)
            {
                query = query.Where(s => s.Group.ClassRoomId == classRoomId.Value);
            }

            var students = await query.ToListAsync();

            return Results.Ok(students.Select(s => new StudentResponseDto(
                s.Id, s.StudentCode, s.Name, s.GitHubUsername, s.AvatarUrl, s.Email, s.IsLeader,
                s.Group?.GroupName, s.GroupId)));
        }).WithName("GetAllStudents");

        group.MapGet("/{id}", async (int id, ClassroomDbContext db) =>
        {
            var s = await db.Students.Include(st => st.Group).FirstOrDefaultAsync(st => st.Id == id);
            if (s == null) return Results.NotFound();
            
            return Results.Ok(new StudentResponseDto(s.Id, s.StudentCode, s.Name, s.GitHubUsername, s.AvatarUrl, s.Email, s.IsLeader, s.Group?.GroupName, s.GroupId));
        }).WithName("GetStudentById");

        group.MapPut("/{id}", async (int id, UpdateStudentDto dto, ClassroomDbContext db) =>
        {
            var s = await db.Students.FindAsync(id);
            if (s == null) return Results.NotFound();
            
            if (dto.Name != null) s.Name = dto.Name;
            if (dto.GitHubUsername != null) s.GitHubUsername = dto.GitHubUsername;
            if (dto.Email != null) s.Email = dto.Email;
            if (dto.IsLeader.HasValue) s.IsLeader = dto.IsLeader.Value;
            
            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Student updated." });
        }).WithName("UpdateStudent");

        group.MapDelete("/{id}", async (int id, ClassroomDbContext db) =>
        {
            var s = await db.Students.FindAsync(id);
            if (s == null) return Results.NotFound();
            
            db.Students.Remove(s);
            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Student deleted." });
        }).WithName("DeleteStudent");
    }
}
