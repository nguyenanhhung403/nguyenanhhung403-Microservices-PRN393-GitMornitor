using System.Text.Json.Serialization;
using Refit;

namespace GitStudentMonitorApi.Services;

public interface IGitHubApi
{
    [Get("/repos/{owner}/{repo}/commits")]
    Task<IEnumerable<GitHubCommit>> GetCommitsAsync(
        string owner, 
        string repo, 
        [AliasAs("per_page")] int perPage = 100, 
        [AliasAs("page")] int page = 1,
        [AliasAs("author")] string? author = null, 
        [AliasAs("since")] string? since = null);

    [Get("/repos/{owner}/{repo}/stats/contributors")]
    Task<List<ContributorStats>?> GetContributorStatsAsync(string owner, string repo);
}

public class GitHubCommit
{
    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;

    [JsonPropertyName("commit")]
    public CommitInfo Commit { get; set; } = new();

    [JsonPropertyName("author")]
    public GitHubAuthor? Author { get; set; }

    [JsonPropertyName("parents")]
    public List<object> Parents { get; set; } = new();
}

public class CommitInfo
{
    [JsonPropertyName("author")]
    public CommitAuthor Author { get; set; } = new();

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class CommitAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}

public class GitHubAuthor
{
    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }
}

// Models for /repos/{owner}/{repo}/stats/contributors
public class ContributorStats
{
    [JsonPropertyName("author")]
    public GitHubAuthor? Author { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("weeks")]
    public List<WeekStats> Weeks { get; set; } = new();
}

public class WeekStats
{
    [JsonPropertyName("w")]
    public long W { get; set; } // Unix timestamp

    [JsonPropertyName("a")]
    public int A { get; set; } // Additions

    [JsonPropertyName("d")]
    public int D { get; set; } // Deletions

    [JsonPropertyName("c")]
    public int C { get; set; } // Commits
}
