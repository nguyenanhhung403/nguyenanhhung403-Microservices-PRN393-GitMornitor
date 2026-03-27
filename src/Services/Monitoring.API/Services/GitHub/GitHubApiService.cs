namespace Monitoring.API.Services.GitHub;

public interface IGitHubApiService
{
    Task<IEnumerable<GitHubCommitResult>> GetCommitsAsync(string owner, string repo, string? token = null);
    Task<IEnumerable<ContributorStatsResult>?> GetContributorStatsAsync(string owner, string repo, string? token = null);
    Task<IDictionary<string, DateTime>> GetRepositoriesPushedAtAsync(IEnumerable<(string owner, string repo)> repos, string? token = null);
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
        _tokenProvider.CurrentToken = token; // may be null for public repos

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
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // Token might be bad — retry without token for public repos
            if (!string.IsNullOrEmpty(token))
            {
                _tokenProvider.CurrentToken = null;
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
                catch (Refit.ApiException innerEx) when (innerEx.StatusCode == System.Net.HttpStatusCode.Forbidden && (innerEx.Content?.Contains("rate limit") == true))
                {
                    throw new Exception("GitHub API rate limit exceeded for unauthenticated requests. Please configure a valid GitHub token.");
                }
                catch { /* If also fails without token, repo is truly private */ }
            }

            if (ex.Content != null && ex.Content.Contains("rate limit"))
                throw new Exception("GitHub API rate limit exceeded.");

            // Extract exact message if possible
            string errorDetails = "Invalid or expired GitHub token.";
            if (!string.IsNullOrEmpty(ex.Content))
                errorDetails += $" Details: {ex.Content}";

            throw new UnauthorizedAccessException(errorDetails);
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Enumerable.Empty<GitHubCommitResult>();
        }
    }

    public async Task<IEnumerable<ContributorStatsResult>?> GetContributorStatsAsync(string owner, string repo, string? token = null)
    {
        _tokenProvider.CurrentToken = token;

        // GitHub returns 202 Accepted while computing stats; retry up to 10 times
        const int maxRetries = 10;
        const int delayMs = 5000;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var stats = await _refitApi.GetContributorStatsAsync(owner, repo);
                if (stats == null || stats.Count == 0) continue; // retry if empty, might be computing

                return stats.Select(s => new ContributorStatsResult
                {
                    Username = s.Author?.Login ?? "Unknown",
                    AvatarUrl = s.Author?.AvatarUrl,
                    TotalCommits = s.Total,
                    TotalAdditions = s.Weeks.Sum(w => w.A),
                    TotalDeletions = s.Weeks.Sum(w => w.D)
                });
            }
            catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                // GitHub is still computing — wait and retry
                await Task.Delay(delayMs);
            }
            catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                // Token might be bad — retry without token for public repos
                if (!string.IsNullOrEmpty(token))
                {
                    _tokenProvider.CurrentToken = null;
                    token = null; // so subsequent retries also use no token
                    continue; // retry this attempt without token
                }

                if (ex.Content != null && ex.Content.Contains("rate limit"))
                    throw new Exception("GitHub API rate limit exceeded. Please configure a valid GitHub token.");

                string errorDetails = "Invalid or expired GitHub token.";
                if (!string.IsNullOrEmpty(ex.Content))
                    errorDetails += $" Details: {ex.Content}";

                throw new UnauthorizedAccessException(errorDetails);
            }
        }

        return null; // exhausted retries
    }

    public async Task<IDictionary<string, DateTime>> GetRepositoriesPushedAtAsync(IEnumerable<(string owner, string repo)> repos, string? token = null)
    {
        var result = new Dictionary<string, DateTime>();
        var repoList = repos.ToList();
        if (!repoList.Any()) return result;

        _tokenProvider.CurrentToken = token;

        // Build a GraphQL query for multiple repositories
        // query {
        //   r0: repository(owner: "owner", name: "repo") { pushedAt }
        //   r1: repository(owner: "owner", name: "repo") { pushedAt }
        // }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("query {");
        for (int i = 0; i < repoList.Count; i++)
        {
            sb.AppendLine($"  r{i}: repository(owner: \"{repoList[i].owner}\", name: \"{repoList[i].repo}\") {{ pushedAt }}");
        }
        sb.AppendLine("}");

        try
        {
            var response = await _refitApi.QueryAsync(new GraphQLRequest { Query = sb.ToString() });
            if (response.Data != null)
            {
                var data = (System.Text.Json.JsonElement)response.Data;
                for (int i = 0; i < repoList.Count; i++)
                {
                    if (data.TryGetProperty($"r{i}", out var repoObj) && repoObj.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        if (repoObj.TryGetProperty("pushedAt", out var pushed) && pushed.TryGetDateTime(out var dt))
                        {
                            result[$"{repoList[i].owner}/{repoList[i].repo}"] = dt;
                        }
                    }
                }
            }
        }
        catch { /* Fallback to empty if GraphQL fails/rate limited */ }

        return result;
    }
}
