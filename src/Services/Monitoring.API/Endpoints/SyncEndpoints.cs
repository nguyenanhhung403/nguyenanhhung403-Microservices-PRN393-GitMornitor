using Monitoring.API.Services;

namespace Monitoring.API.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").WithTags("Sync");

        group.MapGet("/dashboard/{classRoomId}", async (int classRoomId, SyncService syncService) =>
        {
            var result = await syncService.GetDashboardAsync(classRoomId);
            return result != null ? Results.Ok(result) : Results.NotFound("Classroom not found");
        }).WithName("GetDashboard");

        group.MapPost("/sync/{classRoomId}", async (int classRoomId, SyncService syncService) =>
        {
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

        group.MapPost("/sync/group/{groupId}", async (int groupId, SyncService syncService) =>
        {
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

        group.MapGet("/sync-history/{classRoomId}", async (int classRoomId, SyncService syncService) =>
        {
            var result = await syncService.GetSyncHistoryAsync(classRoomId);
            return Results.Ok(result);
        }).WithName("GetSyncHistory");
    }
}
