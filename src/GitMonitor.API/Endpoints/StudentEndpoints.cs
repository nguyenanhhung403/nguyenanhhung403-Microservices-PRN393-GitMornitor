using GitMonitor.Application.DTOs;
using GitMonitor.Domain.Interfaces;

namespace GitMonitor.API.Endpoints;

public static class StudentEndpoints
{
    public static void MapStudentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/students").WithTags("Students");

        group.MapGet("/", async (int? classRoomId, IStudentRepository repo) =>
        {
            var students = classRoomId.HasValue
                ? await repo.GetByClassRoomIdAsync(classRoomId.Value)
                : await repo.GetAllAsync();

            return Results.Ok(students.Select(s => new StudentResponseDto(
                s.Id, s.StudentCode, s.Name, s.GitHubUsername, s.AvatarUrl, s.Email, s.IsLeader,
                s.Group?.GroupName, s.GroupId)));
        }).WithName("GetAllStudents");

        group.MapGet("/{id}", async (int id, IStudentRepository repo) =>
        {
            var s = await repo.GetByIdAsync(id);
            if (s == null) return Results.NotFound();
            return Results.Ok(new StudentResponseDto(s.Id, s.StudentCode, s.Name, s.GitHubUsername, s.AvatarUrl, s.Email, s.IsLeader, s.Group?.GroupName, s.GroupId));
        }).WithName("GetStudentById");

        group.MapPut("/{id}", async (int id, UpdateStudentDto dto, IStudentRepository repo) =>
        {
            var s = await repo.GetByIdAsync(id);
            if (s == null) return Results.NotFound();
            if (dto.Name != null) s.Name = dto.Name;
            if (dto.GitHubUsername != null) s.GitHubUsername = dto.GitHubUsername;
            if (dto.Email != null) s.Email = dto.Email;
            if (dto.IsLeader.HasValue) s.IsLeader = dto.IsLeader.Value;
            await repo.UpdateAsync(s);
            return Results.Ok(new { Message = "Student updated." });
        }).WithName("UpdateStudent");

        group.MapDelete("/{id}", async (int id, IStudentRepository repo) =>
        {
            var s = await repo.GetByIdAsync(id);
            if (s == null) return Results.NotFound();
            await repo.DeleteAsync(id);
            return Results.Ok(new { Message = "Student deleted." });
        }).WithName("DeleteStudent");
    }
}
