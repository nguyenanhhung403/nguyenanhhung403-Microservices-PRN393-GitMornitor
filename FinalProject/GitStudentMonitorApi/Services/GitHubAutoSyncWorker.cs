using GitStudentMonitorApi.Models;
using Microsoft.EntityFrameworkCore;
using Refit;

namespace GitStudentMonitorApi.Services;

public class GitHubAutoSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GitHubAutoSyncWorker> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromHours(1);

    public GitHubAutoSyncWorker(IServiceProvider serviceProvider, ILogger<GitHubAutoSyncWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GitHub Auto Sync Worker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation($"GitHub Auto Sync executing at: {DateTimeOffset.Now}");
            
            try 
            {
                await PerformSyncAsync(stoppingToken);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error occurred during background sync.");
            }

            // Wait until next run
            await Task.Delay(_syncInterval, stoppingToken);
        }
    }

    private async Task PerformSyncAsync(CancellationToken cancellationToken)
    {
        // Scope is required because DbContext and IGitHubApi are scoped services
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GitDbContext>();
        var github = scope.ServiceProvider.GetRequiredService<IGitHubApi>();
        var tokenProvider = scope.ServiceProvider.GetRequiredService<GitHubTokenProvider>();

        var activeClassrooms = await db.ClassRooms
            .Include(c => c.StudentGroups)
            .ThenInclude(g => g.Students)
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);

        int totalGroupsSynced = 0;
        int totalStudentsSynced = 0;
        var batchId = $"AUTO_SYNC_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

        foreach (var classroom in activeClassrooms)
        {
            foreach (var group in classroom.StudentGroups)
            {
                if (cancellationToken.IsCancellationRequested) return;
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
                    catch { /* Stats API may fail for free private repos */ }

                    string groupContributorsJson;

                    if (contributorStats != null && contributorStats.Count > 0)
                    {
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

                        // Update student avatar
                        if (student.AvatarUrl == null)
                        {
                            var statsEntry = contributorStats?.FirstOrDefault(cs => 
                                cs.Author?.Login != null && cs.Author.Login.Equals(student.GitHubUsername, StringComparison.OrdinalIgnoreCase));
                            if (statsEntry?.Author?.AvatarUrl != null)
                                student.AvatarUrl = statsEntry.Author.AvatarUrl;
                            else
                            {
                                var commitWithAvatar = allFilteredCommits.FirstOrDefault(c => 
                                    c.Author?.Login != null && c.Author.Login.Equals(student.GitHubUsername, StringComparison.OrdinalIgnoreCase) 
                                    && c.Author.AvatarUrl != null);
                                if (commitWithAvatar?.Author?.AvatarUrl != null)
                                    student.AvatarUrl = commitWithAvatar.Author.AvatarUrl;
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
                        totalStudentsSynced++;
                    }

                    group.Status = 0; // Active
                    group.LastErrorMessage = null;
                    totalGroupsSynced++;
                }
                catch (ApiException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        group.Status = 2; // Rate limit or invalid token
                        group.LastErrorMessage = $"CRITICAL BGW Error: {ex.StatusCode}. Halting sync for classroom {classroom.Id}.";
                        await db.SaveChangesAsync(cancellationToken); // Save error states immediately
                        _logger.LogWarning($"Circuit breaker triggered by group {group.Id} for 401/403. Stop current classroom loop.");
                        break; 
                    }
                    else if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        group.Status = 1;
                        group.LastErrorMessage = "Repo missing or renamed (404).";
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
            } // end group loop
        } // end classroom loop

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation($"Auto sync complete: Synced {totalGroupsSynced} repositories across {totalStudentsSynced} students.");
    }
}
