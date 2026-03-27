using Monitoring.API.Data;
using Monitoring.API.DTOs;
using Monitoring.API.Entities;
using Monitoring.API.Enums;
using Monitoring.API.Services.GitHub;
using Microsoft.EntityFrameworkCore;

namespace Monitoring.API.Services;

public class SyncService
{
    private readonly MonitoringDbContext _db;
    private readonly IGitHubApiService _gitHubApi;

    public SyncService(MonitoringDbContext db, IGitHubApiService gitHubApi)
    {
        _db = db;
        _gitHubApi = gitHubApi;
    }

    public async Task<object> SyncClassRoomAsync(int classRoomId)
    {
        var classRoom = await _db.ClassRooms
            .Include(c => c.StudentGroups)
            .ThenInclude(g => g.Students)
            .FirstOrDefaultAsync(c => c.Id == classRoomId);
            
        if (classRoom == null) throw new Exception("Classroom not found");

        var batchId = Guid.NewGuid().ToString("N");
        var syncHistories = new List<SyncHistory>();
        var errors = new List<string>();

        // --- OPTIMIZATION: BATCH UPDATE CHECK ---
        var repoInfos = classRoom.StudentGroups
            .Where(g => !string.IsNullOrEmpty(g.RepositoryUrl))
            .Select(g => {
                var parts = g.RepositoryUrl.TrimEnd('/').Split('/');
                return new { 
                    Group = g, 
                    Owner = parts[^2], 
                    Repo = parts[^1].Replace(".git", "", StringComparison.OrdinalIgnoreCase) 
                };
            }).ToList();

        // Group by token to call GraphQL once per token
        var groupedByToken = repoInfos.GroupBy(r => r.Group.Token ?? "");
        var pushedAts = new Dictionary<string, DateTime>();

        foreach (var tokenGroup in groupedByToken)
        {
            var token = string.IsNullOrEmpty(tokenGroup.Key) ? null : tokenGroup.Key;
            var batchResults = await _gitHubApi.GetRepositoriesPushedAtAsync(
                tokenGroup.Select(r => (r.Owner, r.Repo)), 
                token
            );
            foreach (var br in batchResults) pushedAts[br.Key] = br.Value;
        }

        var classroomFallbackToken = classRoom.StudentGroups.FirstOrDefault(g => !string.IsNullOrEmpty(g.Token))?.Token;

        foreach (var info in repoInfos)
        {
            pushedAts.TryGetValue($"{info.Owner}/{info.Repo}", out var latestPush);
            await SyncGroupInternalAsync(info.Group, info.Owner, info.Repo, latestPush, classroomFallbackToken, batchId, syncHistories, errors);
        }

        _db.SyncHistories.AddRange(syncHistories);
        await _db.SaveChangesAsync();

        return new { Message = $"Sync finished. {syncHistories.Count} records synced.", BatchId = batchId, RecordsUpdated = syncHistories.Count, Errors = errors };
    }

    public async Task<object> SyncStudentGroupAsync(int groupId)
    {
        var group = await _db.StudentGroups
            .Include(g => g.Students)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null) throw new Exception("Group not found");

        var batchId = Guid.NewGuid().ToString("N");
        var syncHistories = new List<SyncHistory>();
        var errors = new List<string>();

        if (!string.IsNullOrEmpty(group.RepositoryUrl))
        {
            var parts = group.RepositoryUrl.TrimEnd('/').Split('/');
            var owner = parts[^2];
            var repo = parts[^1].Replace(".git", "", StringComparison.OrdinalIgnoreCase);

            var pushedAts = await _gitHubApi.GetRepositoriesPushedAtAsync(new[] { (owner, repo) }, group.Token);
            pushedAts.TryGetValue($"{owner}/{repo}", out var latestPush);

            await SyncGroupInternalAsync(group, owner, repo, latestPush, null, batchId, syncHistories, errors);
        }

        _db.SyncHistories.AddRange(syncHistories);
        await _db.SaveChangesAsync();

        return new { Message = $"Group sync finished. {syncHistories.Count} records synced.", BatchId = batchId, RecordsUpdated = syncHistories.Count, Errors = errors };
    }

    private async Task SyncGroupInternalAsync(StudentGroup group, string owner, string repo, DateTime latestPush, string? fallbackToken, string batchId, List<SyncHistory> syncHistories, List<string> errors)
    {
        // --- CONDITIONAL SYNC CHECK ---
        if (latestPush != default && group.LastSyncPushedAt.HasValue && latestPush <= group.LastSyncPushedAt.Value)
        {
            return; // Skip expensive sync if repo hasn't changed
        }

        var token = group.Token ?? fallbackToken;
        if (string.IsNullOrEmpty(group.Token) && !string.IsNullOrEmpty(fallbackToken)) group.Token = fallbackToken;

        try
        {
            // Try stats API first (has lines added/deleted data)
            var stats = await _gitHubApi.GetContributorStatsAsync(owner, repo, token);
            if (stats != null && stats.Any())
            {
                foreach (var stat in stats)
                {
                    var student = group.Students.FirstOrDefault(s => s.GitHubUsername.Equals(stat.Username, StringComparison.OrdinalIgnoreCase));
                    if (student == null)
                    {
                        student = new Student
                        {
                            GroupId = group.Id,
                            Name = stat.Username,
                            GitHubUsername = stat.Username,
                            StudentCode = $"EXT-{Guid.NewGuid().ToString("N")[..8]}",
                            AvatarUrl = stat.AvatarUrl
                        };
                        _db.Students.Add(student);
                        await _db.SaveChangesAsync(); // Generate ID
                    }

                    student.AvatarUrl = stat.AvatarUrl;
                    syncHistories.Add(new SyncHistory
                    {
                        BatchId = batchId,
                        StudentId = student.Id,
                        SyncTime = DateTime.UtcNow,
                        CommitCount = stat.TotalCommits,
                        LinesAdded = stat.TotalAdditions,
                        LinesDeleted = stat.TotalDeletions
                    });
                }
            }
            else
            {
                // Fallback to commits API if stats unavailable
                var commits = await _gitHubApi.GetCommitsAsync(owner, repo, token);
                var groupedCommits = commits.GroupBy(c => c.AuthorLogin);
                foreach (var g in groupedCommits)
                {
                    var student = group.Students.FirstOrDefault(s => s.GitHubUsername.Equals(g.Key, StringComparison.OrdinalIgnoreCase));
                    if (student == null)
                    {
                        student = new Student
                        {
                            GroupId = group.Id,
                            Name = g.Key,
                            GitHubUsername = g.Key,
                            StudentCode = $"EXT-{Guid.NewGuid().ToString("N")[..8]}",
                            AvatarUrl = g.First().AuthorAvatarUrl
                        };
                        _db.Students.Add(student);
                        await _db.SaveChangesAsync();
                    }

                    student.AvatarUrl = g.First().AuthorAvatarUrl;
                    syncHistories.Add(new SyncHistory
                    {
                        BatchId = batchId,
                        StudentId = student.Id,
                        SyncTime = DateTime.UtcNow,
                        CommitCount = g.Count(),
                        LinesAdded = 0,
                        LinesDeleted = 0,
                        LastCommitDate = g.Max(c => c.Date)
                    });
                }
            }
            group.Status = GroupStatus.Active;
            group.LastErrorMessage = null;
            group.LastSyncPushedAt = latestPush;
        }
        catch (UnauthorizedAccessException ex)
        {
            group.Status = GroupStatus.Unauthorized;
            group.LastErrorMessage = ex.Message;
            errors.Add($"{group.GroupName}: {ex.Message}");
        }
        catch (Exception ex)
        {
            group.Status = GroupStatus.RepoNotFound;
            group.LastErrorMessage = ex.Message;
            errors.Add($"{group.GroupName}: {ex.Message}");
        }
    }


    public async Task<DashboardResponseDto?> GetDashboardAsync(int classRoomId)
    {
        var classRoom = await _db.ClassRooms
            .Include(c => c.StudentGroups)
            .ThenInclude(g => g.Students)
            .FirstOrDefaultAsync(c => c.Id == classRoomId);
            
        if (classRoom == null) return null;

        var allStudentIds = classRoom.StudentGroups.SelectMany(g => g.Students).Select(s => s.Id).ToList();
        
        var latestSyncs = await _db.SyncHistories
            .Where(sh => allStudentIds.Contains(sh.StudentId))
            .GroupBy(sh => sh.StudentId)
            .Select(g => g.OrderByDescending(sh => sh.SyncTime).FirstOrDefault())
            .ToListAsync();
            
        var syncDict = latestSyncs.Where(sh => sh != null).ToDictionary(sh => sh!.StudentId);

        var leaderboard = new List<LeaderboardItemDto>();
        var repositories = new List<RepositoryItemDto>();

        foreach (var group in classRoom.StudentGroups)
        {
            var contributors = new List<object>();

            foreach (var student in group.Students)
            {
                syncDict.TryGetValue(student.Id, out var sync);

                bool isExternal = student.StudentCode.StartsWith("EXT-");

                contributors.Add(new
                {
                    username = student.GitHubUsername,
                    avatar = student.AvatarUrl,
                    commits = sync?.CommitCount ?? 0,
                    linesAdded = sync?.LinesAdded ?? 0,
                    linesDeleted = sync?.LinesDeleted ?? 0,
                    isExternal = isExternal
                });

                if (!isExternal)
                {
                    leaderboard.Add(new LeaderboardItemDto(
                        student.Name,
                        student.GitHubUsername,
                        student.AvatarUrl,
                        student.StudentCode,
                        group.GroupName,
                        sync?.CommitCount ?? 0,
                        sync?.LinesAdded ?? 0,
                        sync?.LinesDeleted ?? 0,
                        sync?.SyncTime
                    ));
                }
            }

            repositories.Add(new RepositoryItemDto(
                group.GroupName,
                group.RepositoryUrl,
                group.Status.ToString(),
                group.LastErrorMessage,
                contributors
            ));
        }

        return new DashboardResponseDto(
            classRoom.Name,
            leaderboard.OrderByDescending(x => x.CommitCount).ToList(),
            repositories
        );
    }

    public async Task<object> GetSyncHistoryAsync(int classRoomId)
    {
        var classRoom = await _db.ClassRooms
            .Include(c => c.StudentGroups)
            .ThenInclude(g => g.Students)
            .FirstOrDefaultAsync(c => c.Id == classRoomId);

        if (classRoom == null) return new { batches = Array.Empty<object>() };

        var allStudentIds = classRoom.StudentGroups.SelectMany(g => g.Students).Select(s => s.Id).ToList();

        var batches = await _db.SyncHistories
            .Where(sh => allStudentIds.Contains(sh.StudentId))
            .GroupBy(sh => new { sh.BatchId })
            .Select(g => new
            {
                batchId = g.Key.BatchId,
                syncTime = g.Max(sh => sh.SyncTime),
                totalRecords = g.Count(),
                totalCommits = g.Sum(sh => sh.CommitCount),
                totalLinesAdded = g.Sum(sh => sh.LinesAdded),
                totalLinesDeleted = g.Sum(sh => sh.LinesDeleted)
            })
            .OrderByDescending(b => b.syncTime)
            .Take(20)
            .ToListAsync();

        return new { batches };
    }
}
