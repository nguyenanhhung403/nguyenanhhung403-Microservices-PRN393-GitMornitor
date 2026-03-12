using GitMonitor.Application.DTOs;
using GitMonitor.Domain.Interfaces;
using GitMonitor.Domain.Entities;

namespace GitMonitor.API.Endpoints;

public static class TeacherEndpoints
{
    public static void MapTeacherEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/teachers").WithTags("Teachers");

        group.MapGet("/", async (ITeacherRepository repo) =>
        {
            var teachers = await repo.GetAllAsync();
            return Results.Ok(teachers.Select(t => new TeacherResponseDto(t.Id, t.Username, t.Name, t.Email, t.LastLogin)));
        }).WithName("GetAllTeachers");

        group.MapGet("/{id}", async (int id, ITeacherRepository repo) =>
        {
            var t = await repo.GetByIdAsync(id);
            return t != null ? Results.Ok(new TeacherResponseDto(t.Id, t.Username, t.Name, t.Email, t.LastLogin)) : Results.NotFound();
        }).WithName("GetTeacherById");

        group.MapPost("/", async (CreateTeacherDto dto, ITeacherRepository repo) =>
        {
            if (await repo.GetByUsernameAsync(dto.Username) != null)
                return Results.Conflict("Username already exists.");

            var teacher = new Teacher { Username = dto.Username, Name = dto.Name, Email = dto.Email, LastLogin = DateTime.UtcNow };
            await repo.CreateAsync(teacher);
            return Results.Created($"/api/teachers/{teacher.Id}", new { teacher.Id, Message = "Teacher created." });
        }).WithName("CreateTeacher");

        group.MapPut("/{id}", async (int id, UpdateTeacherDto dto, ITeacherRepository repo) =>
        {
            var teacher = await repo.GetByIdAsync(id);
            if (teacher == null) return Results.NotFound();
            if (dto.Name != null) teacher.Name = dto.Name;
            if (dto.Email != null) teacher.Email = dto.Email;
            await repo.UpdateAsync(teacher);
            return Results.Ok(new { Message = "Teacher updated." });
        }).WithName("UpdateTeacher");

        group.MapDelete("/{id}", async (int id, ITeacherRepository repo) =>
        {
            var teacher = await repo.GetByIdAsync(id);
            if (teacher == null) return Results.NotFound();
            await repo.DeleteAsync(id);
            return Results.Ok(new { Message = "Teacher deleted." });
        }).WithName("DeleteTeacher");
    }
}
