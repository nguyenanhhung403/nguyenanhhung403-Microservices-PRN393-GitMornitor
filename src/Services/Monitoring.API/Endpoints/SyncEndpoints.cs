using Monitoring.API.Data;
using Monitoring.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Monitoring.API.Endpoints;

public static class SyncEndpoints
{
    private static bool TryGetTeacherId(HttpContext httpContext, out int teacherId)
    {
        teacherId = 0;
        return int.TryParse(httpContext.Request.Headers["X-Teacher-Id"].FirstOrDefault(), out teacherId);
    }

    public static void MapSyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").WithTags("Sync");

        group.MapGet("/dashboard/{classRoomId}", async (int classRoomId, HttpContext httpContext, SyncService syncService, MonitoringDbContext db) =>
        {
            if (!TryGetTeacherId(httpContext, out var teacherId))
                return Results.Unauthorized();

            var classroom = await db.ClassRooms.FindAsync(classRoomId);
            if (classroom == null) return Results.NotFound("Classroom not found");
            if (classroom.TeacherId != teacherId) return Results.Forbid();

            var result = await syncService.GetDashboardAsync(classRoomId);
            return result != null ? Results.Ok(result) : Results.NotFound("Classroom not found");
        }).WithName("GetDashboard");

        group.MapPost("/sync/{classRoomId}", async (int classRoomId, HttpContext httpContext, SyncService syncService, MonitoringDbContext db) =>
        {
            if (!TryGetTeacherId(httpContext, out var teacherId))
                return Results.Unauthorized();

            var classroom = await db.ClassRooms.FindAsync(classRoomId);
            if (classroom == null) return Results.NotFound("Classroom not found");
            if (classroom.TeacherId != teacherId) return Results.Forbid();

            try
            {
                var result = await syncService.SyncClassRoomAsync(classRoomId);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
                return Results.BadRequest(errorMsg);
            }
        }).WithName("SyncGitHubData");

        group.MapPost("/sync/group/{groupId}", async (int groupId, HttpContext httpContext, SyncService syncService, MonitoringDbContext db) =>
        {
            if (!TryGetTeacherId(httpContext, out var teacherId))
                return Results.Unauthorized();

            var studentGroup = await db.StudentGroups.Include(g => g.ClassRoom).FirstOrDefaultAsync(g => g.Id == groupId);
            if (studentGroup == null) return Results.NotFound("Group not found");
            if (studentGroup.ClassRoom?.TeacherId != teacherId) return Results.Forbid();

            try
            {
                var result = await syncService.SyncStudentGroupAsync(groupId);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
                return Results.BadRequest(errorMsg);
            }
        }).WithName("SyncGroupGitHubData");

        group.MapGet("/sync-history/{classRoomId}", async (int classRoomId, HttpContext httpContext, SyncService syncService, MonitoringDbContext db) =>
        {
            if (!TryGetTeacherId(httpContext, out var teacherId))
                return Results.Unauthorized();

            var classroom = await db.ClassRooms.FindAsync(classRoomId);
            if (classroom == null) return Results.NotFound("Classroom not found");
            if (classroom.TeacherId != teacherId) return Results.Forbid();

            var result = await syncService.GetSyncHistoryAsync(classRoomId);
            return Results.Ok(result);
        }).WithName("GetSyncHistory");
    }
}
