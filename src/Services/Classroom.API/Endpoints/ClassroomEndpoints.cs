using Classroom.API.DTOs;
using Classroom.API.Entities;
using Classroom.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Classroom.API.Endpoints;

public static class ClassRoomEndpoints
{
    public static void MapClassRoomEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/classrooms").WithTags("Classrooms");

        group.MapGet("/", async (ClassroomDbContext db) =>
        {
            var classRooms = await db.ClassRooms
                .Include(c => c.StudentGroups)
                .ThenInclude(g => g.Students)
                .ToListAsync();
            
            return Results.Ok(classRooms.Select(c => new ClassRoomResponseDto(
                c.Id, c.Name, c.TeacherId, c.IsActive,
                c.StudentGroups.Count,
                c.StudentGroups.SelectMany(g => g.Students).Count())));
        }).WithName("GetAllClassrooms");

        group.MapGet("/{id}", async (int id, ClassroomDbContext db) =>
        {
            var c = await db.ClassRooms
                .Include(c => c.StudentGroups)
                .ThenInclude(g => g.Students)
                .FirstOrDefaultAsync(c => c.Id == id);
                
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

        group.MapPost("/", async (CreateClassRoomDto dto, ClassroomDbContext db) =>
        {
            // Note: In a pure microservice, we'd call Identity.API to verify TeacherId.
            // For now, we trust the input since we share the DB context conceptually.
            var classroom = new ClassRoom { Name = dto.Name, TeacherId = dto.TeacherId, IsActive = true };
            db.ClassRooms.Add(classroom);
            await db.SaveChangesAsync();
            return Results.Created($"/api/classrooms/{classroom.Id}", new { classroom.Id, Message = "Classroom created." });
        }).WithName("CreateClassroom");

        group.MapPut("/{id}", async (int id, UpdateClassRoomDto dto, ClassroomDbContext db) =>
        {
            var c = await db.ClassRooms.FindAsync(id);
            if (c == null) return Results.NotFound();
            
            if (dto.Name != null) c.Name = dto.Name;
            if (dto.IsActive.HasValue) c.IsActive = dto.IsActive.Value;
            
            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Classroom updated." });
        }).WithName("UpdateClassroom");

        group.MapDelete("/{id}", async (int id, ClassroomDbContext db) =>
        {
            var c = await db.ClassRooms.FindAsync(id);
            if (c == null) return Results.NotFound();
            
            db.ClassRooms.Remove(c);
            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Classroom deleted." });
        }).WithName("DeleteClassroom");

        // ── Token ──
        group.MapPut("/{classRoomId}/token", async (int classRoomId, TokenDto dto, ClassroomDbContext db) =>
        {
            var classroom = await db.ClassRooms.Include(c => c.StudentGroups).FirstOrDefaultAsync(c => c.Id == classRoomId);
            if (classroom == null) return Results.NotFound();
            
            foreach (var g in classroom.StudentGroups) g.Token = dto.Token;
            await db.SaveChangesAsync();
            return Results.Ok(new { Message = $"Token applied to {classroom.StudentGroups.Count} groups." });
        }).WithName("ConfigureToken");

        // ── Import Students ──
        group.MapPost("/{classRoomId}/import", async (int classRoomId, List<ImportStudentDto> students, ClassroomDbContext db) =>
        {
            var classroom = await db.ClassRooms.FindAsync(classRoomId);
            if (classroom == null) return Results.NotFound("Classroom not found.");

            var existingCodes = await db.Students
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
                    // Inherit token from any existing group in the same classroom
                    var existingToken = await db.StudentGroups
                        .Where(g => g.ClassRoomId == classRoomId && g.Token != null)
                        .Select(g => g.Token)
                        .FirstOrDefaultAsync();

                    existingGroup = new StudentGroup 
                    { 
                        ClassRoomId = classRoomId, 
                        GroupName = groupName, 
                        RepositoryUrl = item.RepositoryUrl,
                        Token = existingToken
                    };
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
        group.MapDelete("/{classRoomId}/students/{studentId}", async (int classRoomId, int studentId, ClassroomDbContext db) =>
        {
            var student = await db.Students
                .Include(s => s.Group)
                .FirstOrDefaultAsync(s => s.Id == studentId && s.Group.ClassRoomId == classRoomId);
                
            if (student == null) return Results.NotFound("Student not found in this classroom.");

            db.Students.Remove(student);
            await db.SaveChangesAsync();
            
            return Results.Ok(new { Message = "Student removed from classroom." });
        }).WithName("RemoveStudentFromClassroom");
    }
}
