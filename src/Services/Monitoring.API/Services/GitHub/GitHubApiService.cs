namespace Monitoring.API.Services.GitHub;

public interface IGitHubApiService
{
    Task<IEnumerable<GitHubCommitResult>> GetCommitsAsync(string owner, string repo, string? token = null);
    Task<IEnumerable<ContributorStatsResult>?> GetContributorStatsAsync(string owner, string repo, string? token = null);
}

public class GitHubApiService : IGitHubApiService
{
    private readonly IGitHubApi _refitApi;
    private readonly GitHubTokenProvider _tokenProvider;

    public GitHubApiService(IGitHubApi refitApi, GitHubTokenProvider tokenProvider)
    {
        _refitApi = refitApi;
        _tokenProvider = tokenProvider;
    }

    public async Task<IEnumerable<GitHubCommitResult>> GetCommitsAsync(string owner, string repo, string? token = null)
    {
        if (!string.IsNullOrEmpty(token)) _tokenProvider.CurrentToken = token;

        try
        {
            var commits = await _refitApi.GetCommitsAsync(owner, repo);
            return commits.Select(c => new GitHubCommitResult
            {
                Sha = c.Sha,
                AuthorLogin = c.Author?.Login ?? "Unknown",
                AuthorName = c.Commit.Author.Name,
                AuthorAvatarUrl = c.Author?.AvatarUrl,
                Message = c.Commit.Message,
                Date = c.Commit.Author.Date,
                IsMergeCommit = c.Parents.Count > 1
            });
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Enumerable.Empty<GitHubCommitResult>();
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("Invalid or expired GitHub token.");
        }
    }

    public async Task<IEnumerable<ContributorStatsResult>?> GetContributorStatsAsync(string owner, string repo, string? token = null)
    {
        if (!string.IsNullOrEmpty(token)) _tokenProvider.CurrentToken = token;

        try
        {
            var stats = await _refitApi.GetContributorStatsAsync(owner, repo);
            if (stats == null) return null;

            return stats.Select(s => new ContributorStatsResult
            {
                Username = s.Author?.Login ?? "Unknown",
                AvatarUrl = s.Author?.AvatarUrl,
                TotalCommits = s.Total,
                TotalAdditions = s.Weeks.Sum(w => w.A),
                TotalDeletions = s.Weeks.Sum(w => w.D)
            });
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            return null;
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
             throw new UnauthorizedAccessException("Invalid or expired GitHub token.");
        }
    }
}
