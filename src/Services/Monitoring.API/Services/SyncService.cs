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
        
        foreach (var group in classRoom.StudentGroups)
        {
            if (string.IsNullOrEmpty(group.RepositoryUrl)) continue;

            var parts = group.RepositoryUrl.TrimEnd('/').Split('/');
            if (parts.Length < 2) continue;
            var owner = parts[^2];
            var repo = parts[^1].Replace(".git", "", StringComparison.OrdinalIgnoreCase);

            try
            {
                var stats = await _gitHubApi.GetContributorStatsAsync(owner, repo, group.Token);
                if (stats != null)
                {
                    foreach (var stat in stats)
                    {
                        var student = group.Students.FirstOrDefault(s => s.GitHubUsername.Equals(stat.Username, StringComparison.OrdinalIgnoreCase));
                        if (student != null)
                        {
                            student.AvatarUrl = stat.AvatarUrl; // update avatar in shared table
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
                }
                else
                {
                    var commits = await _gitHubApi.GetCommitsAsync(owner, repo, group.Token);
                    var grouped = commits.GroupBy(c => c.AuthorLogin);
                    foreach (var g in grouped)
                    {
                        var student = group.Students.FirstOrDefault(s => s.GitHubUsername.Equals(g.Key, StringComparison.OrdinalIgnoreCase));
                        if (student != null)
                        {
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
                }
                group.Status = GroupStatus.Active;
                group.LastErrorMessage = null;
            }
            catch (UnauthorizedAccessException ex)
            {
                group.Status = GroupStatus.Unauthorized;
                group.LastErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                group.Status = GroupStatus.RepoNotFound;
                group.LastErrorMessage = ex.Message;
            }
        }

        if (syncHistories.Any())
        {
            _db.SyncHistories.AddRange(syncHistories);
            // implicitly updates students and groups because they are tracked by EF
            await _db.SaveChangesAsync(); 
        }

        return new { Message = "Sync finished", BatchId = batchId, RecordsUpdated = syncHistories.Count };
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
            var groupStudentUsernames = group.Students.Select(s => s.GitHubUsername.ToLower()).ToHashSet();

            foreach (var student in group.Students)
            {
                syncDict.TryGetValue(student.Id, out var sync);

                contributors.Add(new
                {
                    username = student.GitHubUsername,
                    avatar = student.AvatarUrl,
                    commits = sync?.CommitCount ?? 0,
                    linesAdded = sync?.LinesAdded ?? 0,
                    linesDeleted = sync?.LinesDeleted ?? 0
                });

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

            if (!string.IsNullOrEmpty(group.RepositoryUrl))
            {
                var parts = group.RepositoryUrl.TrimEnd('/').Split('/');
                if (parts.Length >= 2)
                {
                    var owner = parts[^2];
                    var repo = parts[^1].Replace(".git", "", StringComparison.OrdinalIgnoreCase);
                    
                    try
                    {
                        var stats = await _gitHubApi.GetContributorStatsAsync(owner, repo, group.Token);
                        if (stats != null)
                        {
                            foreach (var stat in stats)
                            {
                                if (!groupStudentUsernames.Contains(stat.Username.ToLower()))
                                {
                                    contributors.Add(new
                                    {
                                        username = stat.Username,
                                        avatar = stat.AvatarUrl,
                                        commits = stat.TotalCommits,
                                        linesAdded = stat.TotalAdditions,
                                        linesDeleted = stat.TotalDeletions,
                                        isExternal = true
                                    });
                                }
                            }
                        }
                    }
                    catch { /* Ignore */ }
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
}
