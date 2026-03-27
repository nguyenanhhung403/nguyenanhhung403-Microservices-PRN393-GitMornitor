using GitStudentMonitorApi.Models;
using Microsoft.EntityFrameworkCore;

namespace GitStudentMonitorApi.Endpoints;

public static class ClassroomEndpoints
{
    public static void MapClassroomEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/classrooms").WithTags("Classrooms");

        // GET: List all classrooms
        group.MapGet("/", async (GitDbContext db) =>
        {
            var classrooms = await db.ClassRooms
                .Include(c => c.StudentGroups)
                .ThenInclude(g => g.Students)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.TeacherId,
                    c.IsActive,
                    TotalGroups = c.StudentGroups.Count,
                    TotalStudents = c.StudentGroups.SelectMany(g => g.Students).Count()
                })
                .ToListAsync();
            return Results.Ok(classrooms);
        })
        .WithName("GetAllClassrooms");

        // GET: Get classroom by ID
        group.MapGet("/{id}", async (int id, GitDbContext db) =>
        {
            var classroom = await db.ClassRooms
                .Include(c => c.StudentGroups)
                .ThenInclude(g => g.Students)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (classroom == null) return Results.NotFound("Classroom not found.");

            return Results.Ok(new
            {
                classroom.Id,
                classroom.Name,
                classroom.TeacherId,
                classroom.IsActive,
                Groups = classroom.StudentGroups.Select(g => new
                {
                    g.Id,
                    g.GroupName,
                    g.RepositoryUrl,
                    g.Status,
                    Students = g.Students.Select(s => new { s.Id, s.StudentCode, s.Name, s.GitHubUsername, s.AvatarUrl })
                })
            });
        })
        .WithName("GetClassroomById");

        // POST: Create classroom
        group.MapPost("/", async (ClassroomRequest req, GitDbContext db) =>
        {
            var teacher = await db.Teachers.FindAsync(req.TeacherId);
            if (teacher == null) return Results.NotFound("Teacher not found");

            var classroom = new ClassRoom
            {
                Name = req.Name,
                TeacherId = req.TeacherId,
                IsActive = true
            };
            db.ClassRooms.Add(classroom);
            await db.SaveChangesAsync();

            return Results.Created($"/api/classrooms/{classroom.Id}", new { Message = "Classroom created", ClassRoomId = classroom.Id });
        })
        .WithName("CreateClassroom");

        // PUT: Update classroom
        group.MapPut("/{id}", async (int id, ClassroomUpdateRequest req, GitDbContext db) =>
        {
            var classroom = await db.ClassRooms.FindAsync(id);
            if (classroom == null) return Results.NotFound("Classroom not found.");

            if (req.Name != null) classroom.Name = req.Name;
            if (req.IsActive.HasValue) classroom.IsActive = req.IsActive.Value;

            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Classroom updated.", ClassRoomId = classroom.Id });
        })
        .WithName("UpdateClassroom");

        // DELETE: Delete classroom (cascade deletes groups, students, sync history)
        group.MapDelete("/{id}", async (int id, GitDbContext db) =>
        {
            var classroom = await db.ClassRooms
                .Include(c => c.StudentGroups)
                .ThenInclude(g => g.Students)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (classroom == null) return Results.NotFound("Classroom not found.");

            // Delete sync histories for all students in this classroom
            var studentIds = classroom.StudentGroups.SelectMany(g => g.Students).Select(s => s.Id).ToList();
            var syncHistories = await db.SyncHistories.Where(sh => studentIds.Contains(sh.StudentId)).ToListAsync();
            db.SyncHistories.RemoveRange(syncHistories);

            // Delete students, groups, then classroom
            foreach (var g in classroom.StudentGroups)
            {
                db.Students.RemoveRange(g.Students);
            }
            db.StudentGroups.RemoveRange(classroom.StudentGroups);
            db.ClassRooms.Remove(classroom);

            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Classroom and all related data deleted." });
        })
        .WithName("DeleteClassroom");

        // PUT: Configure Token for all groups in a classroom
        group.MapPut("/{classRoomId}/token", async (int classRoomId, TokenRequest req, GitDbContext db) =>
        {
            var classroom = await db.ClassRooms
                .Include(c => c.StudentGroups)
                .FirstOrDefaultAsync(c => c.Id == classRoomId);

            if (classroom == null) return Results.NotFound("ClassRoom not found");

            foreach (var g in classroom.StudentGroups)
            {
                g.Token = req.Token;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { Message = $"Token applied to {classroom.StudentGroups.Count} groups." });
        })
        .WithName("ConfigureToken");

        // POST: Import students from JSON list
        group.MapPost("/{classRoomId}/import", async (int classRoomId, List<ImportStudentItem> students, GitDbContext db) =>
        {
            var classroom = await db.ClassRooms.FindAsync(classRoomId);
            if (classroom == null) return Results.NotFound("Classroom not found.");

            // Get the current max student code number in this classroom to auto-increment
            var existingCodes = await db.Students
                .Where(s => s.Group != null && s.Group.ClassRoomId == classRoomId)
                .Select(s => s.StudentCode)
                .ToListAsync();

            int maxCode = 0;
            foreach (var code in existingCodes)
            {
                if (code.StartsWith("SV") && int.TryParse(code.Substring(2), out int num))
                    maxCode = Math.Max(maxCode, num);
            }

            var addedStudents = 0;
            var errorLines = new List<string>();

            foreach (var item in students)
            {
                if (string.IsNullOrWhiteSpace(item.UserName) || string.IsNullOrWhiteSpace(item.RepositoryUrl))
                {
                    errorLines.Add($"Missing userName or repositoryUrl for entry: {item.UserName}");
                    continue;
                }

                if (!Uri.TryCreate(item.RepositoryUrl, UriKind.Absolute, out var uri))
                {
                    errorLines.Add($"Invalid URL: {item.RepositoryUrl}");
                    continue;
                }

                var pathSegments = uri.AbsolutePath.TrimStart('/').Split('/');
                if (pathSegments.Length < 2)
                {
                    errorLines.Add($"Invalid Repo URL format: {item.RepositoryUrl}");
                    continue;
                }

                var repoOwner = pathSegments[0];
                var repoName = pathSegments[1];
                var groupName = $"{repoOwner}-{repoName}";

                // Check if group exists, else create it
                var existingGroup = await db.StudentGroups.FirstOrDefaultAsync(g => g.ClassRoomId == classRoomId && g.RepositoryUrl == item.RepositoryUrl);
                if (existingGroup == null)
                {
                    existingGroup = new StudentGroup
                    {
                        ClassRoomId = classRoomId,
                        GroupName = groupName,
                        RepositoryUrl = item.RepositoryUrl,
                        Status = 0
                    };
                    db.StudentGroups.Add(existingGroup);
                    await db.SaveChangesAsync(); // Save to get Group ID
                }

                // Prevent duplicate username in the same group
                if (!await db.Students.AnyAsync(s => s.GitHubUsername == item.UserName && s.GroupId == existingGroup.Id))
                {
                    maxCode++;
                    var studentCode = $"SV{maxCode:D3}";

                    var student = new Student
                    {
                        StudentCode = studentCode,
                        Name = item.UserName,
                        GitHubUsername = item.UserName,
                        GroupId = existingGroup.Id
                    };
                    db.Students.Add(student);
                    addedStudents++;
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new
            {
                Message = $"Import completed. Added {addedStudents} students.",
                Errors = errorLines
            });
        })
        .WithName("ImportStudents");
    }
}
