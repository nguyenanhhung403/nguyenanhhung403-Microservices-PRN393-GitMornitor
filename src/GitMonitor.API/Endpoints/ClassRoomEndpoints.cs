using GitMonitor.Application.DTOs;
using GitMonitor.Domain.Interfaces;
using GitMonitor.Domain.Entities;
using GitMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GitMonitor.API.Endpoints;

public static class ClassRoomEndpoints
{
    public static void MapClassRoomEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/classrooms").WithTags("Classrooms");

        group.MapGet("/", async (IClassRoomRepository repo) =>
        {
            var classRooms = await repo.GetAllAsync();
            return Results.Ok(classRooms.Select(c => new ClassRoomResponseDto(
                c.Id, c.Name, c.TeacherId, c.IsActive,
                c.StudentGroups.Count,
                c.StudentGroups.SelectMany(g => g.Students).Count())));
        }).WithName("GetAllClassrooms");

        group.MapGet("/{id}", async (int id, IClassRoomRepository repo) =>
        {
            var c = await repo.GetByIdWithDetailsAsync(id);
            if (c == null) return Results.NotFound();
            return Results.Ok(new
            {
                c.Id, c.Name, c.TeacherId, c.IsActive,
                Groups = c.StudentGroups.Select(g => new
                {
                    g.Id, g.GroupName, g.RepositoryUrl, Status = g.Status.ToString(),
                    Students = g.Students.Select(s => new { s.Id, s.StudentCode, s.Name, s.GitHubUsername, s.AvatarUrl })
                })
            });
        }).WithName("GetClassroomById");

        group.MapPost("/", async (CreateClassRoomDto dto, ITeacherRepository teacherRepo, IClassRoomRepository repo) =>
        {
            if (await teacherRepo.GetByIdAsync(dto.TeacherId) == null)
                return Results.NotFound("Teacher not found.");

            var classroom = new ClassRoom { Name = dto.Name, TeacherId = dto.TeacherId, IsActive = true };
            await repo.CreateAsync(classroom);
            return Results.Created($"/api/classrooms/{classroom.Id}", new { classroom.Id, Message = "Classroom created." });
        }).WithName("CreateClassroom");

        group.MapPut("/{id}", async (int id, UpdateClassRoomDto dto, IClassRoomRepository repo) =>
        {
            var c = await repo.GetByIdAsync(id);
            if (c == null) return Results.NotFound();
            if (dto.Name != null) c.Name = dto.Name;
            if (dto.IsActive.HasValue) c.IsActive = dto.IsActive.Value;
            await repo.UpdateAsync(c);
            return Results.Ok(new { Message = "Classroom updated." });
        }).WithName("UpdateClassroom");

        group.MapDelete("/{id}", async (int id, IClassRoomRepository repo) =>
        {
            var c = await repo.GetByIdAsync(id);
            if (c == null) return Results.NotFound();
            await repo.DeleteAsync(id);
            return Results.Ok(new { Message = "Classroom deleted." });
        }).WithName("DeleteClassroom");

        // ── Token ──
        group.MapPut("/{classRoomId}/token", async (int classRoomId, TokenDto dto, GitMonitorDbContext db) =>
        {
            var classroom = await db.ClassRooms.Include(c => c.StudentGroups).FirstOrDefaultAsync(c => c.Id == classRoomId);
            if (classroom == null) return Results.NotFound();
            foreach (var g in classroom.StudentGroups) g.Token = dto.Token;
            await db.SaveChangesAsync();
            return Results.Ok(new { Message = $"Token applied to {classroom.StudentGroups.Count} groups." });
        }).WithName("ConfigureToken");

        // ── Import Students ──
        group.MapPost("/{classRoomId}/import", async (int classRoomId, List<ImportStudentDto> students, GitMonitorDbContext db) =>
        {
            var classroom = await db.ClassRooms.FindAsync(classRoomId);
            if (classroom == null) return Results.NotFound("Classroom not found.");

            var existingCodes = await db.Students
                .Where(s => s.Group.ClassRoomId == classRoomId)
                .Select(s => s.StudentCode).ToListAsync();

            int maxCode = 0;
            foreach (var code in existingCodes)
                if (code.StartsWith("SV") && int.TryParse(code.Substring(2), out int num))
                    maxCode = Math.Max(maxCode, num);

            int added = 0;
            var errors = new List<string>();

            foreach (var item in students)
            {
                if (string.IsNullOrWhiteSpace(item.UserName) || string.IsNullOrWhiteSpace(item.RepositoryUrl))
                { errors.Add($"Missing data: {item.UserName}"); continue; }

                if (!Uri.TryCreate(item.RepositoryUrl, UriKind.Absolute, out var uri))
                { errors.Add($"Invalid URL: {item.RepositoryUrl}"); continue; }

                var parts = uri.AbsolutePath.TrimStart('/').Split('/');
                if (parts.Length < 2) { errors.Add($"Bad URL format: {item.RepositoryUrl}"); continue; }

                var groupName = $"{parts[0]}-{parts[1]}";
                var existingGroup = await db.StudentGroups.FirstOrDefaultAsync(g => g.ClassRoomId == classRoomId && g.RepositoryUrl == item.RepositoryUrl);
                if (existingGroup == null)
                {
                    existingGroup = new StudentGroup { ClassRoomId = classRoomId, GroupName = groupName, RepositoryUrl = item.RepositoryUrl };
                    db.StudentGroups.Add(existingGroup);
                    await db.SaveChangesAsync();
                }

                if (!await db.Students.AnyAsync(s => s.GitHubUsername == item.UserName && s.GroupId == existingGroup.Id))
                {
                    maxCode++;
                    db.Students.Add(new Student { StudentCode = $"SV{maxCode:D3}", Name = item.UserName, GitHubUsername = item.UserName, GroupId = existingGroup.Id });
                    added++;
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { Message = $"Added {added} students.", Errors = errors });
        }).WithName("ImportStudents");

        // ── Remove Student from Classroom ──
        group.MapDelete("/{classRoomId}/students/{studentId}", async (int classRoomId, int studentId, GitMonitorDbContext db) =>
        {
            var student = await db.Students
                .Include(s => s.Group)
                .FirstOrDefaultAsync(s => s.Id == studentId && s.Group.ClassRoomId == classRoomId);
                
            if (student == null) return Results.NotFound("Student not found in this classroom.");

            // Also delete sync histories for this student
            var histories = await db.SyncHistories.Where(sh => sh.StudentId == studentId).ToListAsync();
            db.SyncHistories.RemoveRange(histories);
            
            db.Students.Remove(student);
            await db.SaveChangesAsync();
            
            return Results.Ok(new { Message = "Student removed from classroom." });
        }).WithName("RemoveStudentFromClassroom");
    }
}
