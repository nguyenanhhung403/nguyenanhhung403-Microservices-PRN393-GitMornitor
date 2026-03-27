using GitStudentMonitorApi.Models;
using GitStudentMonitorApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Refit;

namespace GitStudentMonitorApi.Endpoints;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this WebApplication app)
    {
        // GET Dashboard
        app.MapGet("/api/dashboard/{classRoomId}", async (int classRoomId, GitDbContext db) =>
        {
            var data = await GetDashboardDataAsync(classRoomId, db);
            return data != null ? Results.Ok(data) : Results.NotFound("ClassRoom not found.");
        })
        .WithName("GetDashboard")
        .WithTags("Sync");

        // POST Sync GitHub Data
        app.MapPost("/api/sync/{classRoomId}", async (int classRoomId, GitDbContext db, IGitHubApi github, IMemoryCache cache, GitHubTokenProvider tokenProvider) =>
        {
            string cacheKey = $"sync_{classRoomId}";

            // 1. Check MemoryCache
            if (cache.TryGetValue(cacheKey, out object? cachedData))
            {
                return Results.Ok(new { Source = "Cache", Data = cachedData });
            }

            var classroom = await db.ClassRooms
                .Include(c => c.StudentGroups)
                .ThenInclude(g => g.Students)
                .FirstOrDefaultAsync(c => c.Id == classRoomId);

            if (classroom == null) return Results.NotFound("ClassRoom not found.");

            // 2. Check DB if data is still fresh (e.g., inside 1 hour).
            var studentIds = classroom.StudentGroups.SelectMany(g => g.Students).Select(s => s.Id).ToList();
            var latestGlobalSync = await db.SyncHistories
                .Where(sh => studentIds.Contains(sh.StudentId))
                .OrderByDescending(sh => sh.SyncTime)
                .FirstOrDefaultAsync();

            if (latestGlobalSync != null && latestGlobalSync.SyncTime > DateTime.UtcNow.AddHours(-1))
            {
                var dashboardResult = await GetDashboardDataAsync(classRoomId, db);
                cache.Set(cacheKey, dashboardResult, TimeSpan.FromHours(1));
                return Results.Ok(new { Source = "Database", Data = dashboardResult });
            }

            // 3. Data is outdated or missing. Use Refit to call GitHub API.
            var batchId = Guid.NewGuid().ToString();

            foreach (var group in classroom.StudentGroups)
            {
                if (string.IsNullOrEmpty(group.RepositoryUrl)) continue;

                try
                {
                    var uri = new Uri(group.RepositoryUrl);
                    var pathParts = uri.AbsolutePath.TrimStart('/').Split('/');
                    if (pathParts.Length < 2) continue;

                    string owner = pathParts[0];
                    string repo = pathParts[1];

                    tokenProvider.Token = group.Token;

                    var allAuthCommits = new List<GitHubCommit>();
                    var allFilteredCommits = new List<GitHubCommit>();
                    int page = 1;

                    while (true)
                    {
                        var commits = await github.GetCommitsAsync(owner, repo, 100, page);
                        if (!commits.Any()) break;

                        // Exclude merge commits (commits with 2 or more parents)
                        var filteredCommits = commits.Where(c => c.Parents == null || c.Parents.Count <= 1).ToList();
                        allFilteredCommits.AddRange(filteredCommits);

                        foreach (var student in group.Students)
                        {
                            var authCommits = filteredCommits.Where(c =>
                                (c.Author?.Login != null && c.Author.Login.Equals(student.GitHubUsername, StringComparison.OrdinalIgnoreCase)) ||
                                (c.Commit?.Author?.Email != null && c.Commit.Author.Email.Equals(student.GitHubUsername, StringComparison.OrdinalIgnoreCase)) ||
                                (c.Commit?.Author?.Name != null && c.Commit.Author.Name.Equals(student.Name, StringComparison.OrdinalIgnoreCase))
                            ).ToList();
                            allAuthCommits.AddRange(authCommits);
                        }

                        if (commits.Count() < 100) break; // Last page
                        page++;
                    }

                    // Fetch contributor stats (additions/deletions) from GitHub Stats API
                    List<ContributorStats>? contributorStats = null;
                    try
                    {
                        contributorStats = await github.GetContributorStatsAsync(owner, repo);
                    }
                    catch { /* Stats API may return 202 (computing) or fail for free private repos */ }

                    string groupContributorsJson;

                    if (contributorStats != null && contributorStats.Count > 0)
                    {
                        // Stats API worked — use full data with additions/deletions
                        var groupContributors = contributorStats
                            .Where(cs => cs.Author != null)
                            .Select(cs => new
                            {
                                username = cs.Author!.Login,
                                avatarUrl = cs.Author.AvatarUrl,
                                commitCount = cs.Total,
                                additions = cs.Weeks.Sum(w => w.A),
                                deletions = cs.Weeks.Sum(w => w.D)
                            })
                            .OrderByDescending(x => x.commitCount)
                            .ToList();
                        groupContributorsJson = System.Text.Json.JsonSerializer.Serialize(groupContributors);
                    }
                    else
                    {
                        // Fallback for private repos (free plan) — build from commit data
                        var fallbackContributors = allFilteredCommits
                            .GroupBy(c => c.Author?.Login ?? c.Commit.Author.Name)
                            .Select(g => new
                            {
                                username = g.Key,
                                avatarUrl = g.FirstOrDefault(c => c.Author?.AvatarUrl != null)?.Author?.AvatarUrl,
                                commitCount = g.Count(),
                                additions = (int?)null,
                                deletions = (int?)null
                            })
                            .OrderByDescending(x => x.commitCount)
                            .ToList();
                        groupContributorsJson = System.Text.Json.JsonSerializer.Serialize(fallbackContributors);
                    }

                    foreach (var student in group.Students)
                    {
                        var studentCommits = allAuthCommits.Where(c =>
                            (c.Author?.Login != null && c.Author.Login.Equals(student.GitHubUsername, StringComparison.OrdinalIgnoreCase)) ||
                            (c.Commit?.Author?.Email != null && c.Commit.Author.Email.Equals(student.GitHubUsername, StringComparison.OrdinalIgnoreCase)) ||
                            (c.Commit?.Author?.Name != null && c.Commit.Author.Name.Equals(student.Name, StringComparison.OrdinalIgnoreCase))
                        ).ToList();

                        // Update student avatar from stats data or from commit author data
                        if (student.AvatarUrl == null)
                        {
                            var statsEntry = contributorStats?.FirstOrDefault(cs =>
                                cs.Author?.Login != null && cs.Author.Login.Equals(student.GitHubUsername, StringComparison.OrdinalIgnoreCase));
                            if (statsEntry?.Author?.AvatarUrl != null)
                            {
                                student.AvatarUrl = statsEntry.Author.AvatarUrl;
                            }
                            else
                            {
                                var commitWithAvatar = allFilteredCommits.FirstOrDefault(c =>
                                    c.Author?.Login != null && c.Author.Login.Equals(student.GitHubUsername, StringComparison.OrdinalIgnoreCase)
                                    && c.Author.AvatarUrl != null);
                                if (commitWithAvatar?.Author?.AvatarUrl != null)
                                {
                                    student.AvatarUrl = commitWithAvatar.Author.AvatarUrl;
                                }
                            }
                        }

                        var syncHistory = new SyncHistory
                        {
                            BatchId = batchId,
                            StudentId = student.Id,
                            CommitCount = studentCommits.Count,
                            RawDataJson = groupContributorsJson,
                            SyncTime = DateTime.UtcNow
                        };

                        db.SyncHistories.Add(syncHistory);
                    }

                    group.Status = 0; // Active
                    group.LastErrorMessage = null;
                }
                catch (ApiException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        group.Status = 2;
                        group.LastErrorMessage = $"CRITICAL API Error: {ex.StatusCode}. Stopping Sync.";
                        break;
                    }
                    else if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        group.Status = 1;
                        group.LastErrorMessage = $"API Error: {ex.StatusCode} - Repo missing or renamed.";
                        continue;
                    }

                    group.Status = 1;
                    group.LastErrorMessage = $"API Error: {ex.StatusCode}";
                }
                catch (Exception ex)
                {
                    group.Status = 1;
                    group.LastErrorMessage = ex.Message;
                }
            }

            await db.SaveChangesAsync();

            // 4. Update Cache and Return Data
            var newDashboardResult = await GetDashboardDataAsync(classRoomId, db);
            cache.Set(cacheKey, newDashboardResult, TimeSpan.FromHours(1));

            return Results.Ok(new { Source = "GitHub API", Data = newDashboardResult });
        })
        .WithName("SyncGitHubData")
        .WithTags("Sync");
    }

    public static async Task<object?> GetDashboardDataAsync(int classRoomId, GitDbContext db)
    {
        var classroom = await db.ClassRooms
            .Include(c => c.StudentGroups)
            .ThenInclude(g => g.Students)
            .FirstOrDefaultAsync(c => c.Id == classRoomId);

        if (classroom == null) return null;

        var studentIds = classroom.StudentGroups.SelectMany(g => g.Students).Select(s => s.Id).ToList();

        var latestSyncs = await db.SyncHistories
            .Where(sh => studentIds.Contains(sh.StudentId))
            .GroupBy(sh => sh.StudentId)
            .Select(g => g.OrderByDescending(x => x.SyncTime).FirstOrDefault())
            .ToListAsync();

        var leaderboard = classroom.StudentGroups
            .SelectMany(g => g.Students)
            .Select(s => new
            {
                s.Name,
                s.GitHubUsername,
                s.AvatarUrl,
                s.StudentCode,
                GroupName = s.Group?.GroupName,
                CommitCount = latestSyncs.FirstOrDefault(ls => ls?.StudentId == s.Id)?.CommitCount ?? 0,
                LastSync = latestSyncs.FirstOrDefault(ls => ls?.StudentId == s.Id)?.SyncTime
            })
            .OrderByDescending(x => x.CommitCount)
            .ToList();

        var repositories = classroom.StudentGroups.Select(g =>
        {
            var firstStudentId = g.Students.FirstOrDefault()?.Id;
            var groupHistory = firstStudentId.HasValue ? latestSyncs.FirstOrDefault(ls => ls?.StudentId == firstStudentId) : null;
            object? contributors = null;
            if (!string.IsNullOrEmpty(groupHistory?.RawDataJson))
            {
                try { contributors = System.Text.Json.JsonSerializer.Deserialize<object>(groupHistory.RawDataJson); } catch { }
            }

            return new
            {
                g.GroupName,
                g.RepositoryUrl,
                Status = g.Status.ToString(),
                g.LastErrorMessage,
                Contributors = contributors
            };
        }).ToList();

        return new
        {
            ClassRoom = classroom.Name,
            Leaderboard = leaderboard,
            Repositories = repositories
        };
    }
}
