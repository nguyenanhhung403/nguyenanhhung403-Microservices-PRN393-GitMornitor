using GitMonitor.Application.Services;

namespace GitMonitor.API.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this WebApplication app)
    {
        // GET Dashboard
        app.MapGet("/api/dashboard/{classRoomId}", async (int classRoomId, ISyncService syncService) =>
        {
            var result = await syncService.GetDashboardAsync(classRoomId);
            return result != null ? Results.Ok(result) : Results.NotFound("Classroom not found");
        })
        .WithName("GetDashboard")
        .WithTags("Sync");

        // POST Sync
        app.MapPost("/api/sync/{classRoomId}", async (int classRoomId, ISyncService syncService) =>
        {
            try
            {
                var result = await syncService.SyncClassRoomAsync(classRoomId);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ex.Message);
            }
        })
        .WithName("SyncGitHubData")
        .WithTags("Sync");
    }
}
