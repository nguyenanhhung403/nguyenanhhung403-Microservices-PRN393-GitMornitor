using System.Text.Json.Serialization;
using Refit;

namespace Monitoring.API.Services.GitHub;

public interface IGitHubApi
{
    [Get("/repos/{owner}/{repo}/commits")]
    Task<IEnumerable<GitHubCommitResponse>> GetCommitsAsync(
        string owner, string repo,
        [AliasAs("per_page")] int perPage = 100,
        [AliasAs("page")] int page = 1);

    [Get("/repos/{owner}/{repo}/stats/contributors")]
    Task<List<ContributorStatsResponse>?> GetContributorStatsAsync(string owner, string repo);
}

// ── Models ──
public class GitHubCommitResponse
{
    [JsonPropertyName("sha")] public string Sha { get; set; } = string.Empty;
    [JsonPropertyName("commit")] public CommitInfoResponse Commit { get; set; } = new();
    [JsonPropertyName("author")] public GitHubAuthorResponse? Author { get; set; }
    [JsonPropertyName("parents")] public List<object> Parents { get; set; } = new();
}

public class CommitInfoResponse
{
    [JsonPropertyName("author")] public CommitAuthorResponse Author { get; set; } = new();
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
}

public class CommitAuthorResponse
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("date")] public DateTime Date { get; set; }
}

public class GitHubAuthorResponse
{
    [JsonPropertyName("login")] public string Login { get; set; } = string.Empty;
    [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; set; }
}

public class ContributorStatsResponse
{
    [JsonPropertyName("author")] public GitHubAuthorResponse? Author { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("weeks")] public List<WeekStatsResponse> Weeks { get; set; } = new();
}

public class WeekStatsResponse
{
    [JsonPropertyName("w")] public long W { get; set; }
    [JsonPropertyName("a")] public int A { get; set; }
    [JsonPropertyName("d")] public int D { get; set; }
    [JsonPropertyName("c")] public int C { get; set; }
}

// ── Service Wrapper Models ──
public class GitHubCommitResult
{
    public string Sha { get; set; } = string.Empty;
    public string AuthorLogin { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatarUrl { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool IsMergeCommit { get; set; }
}

public class ContributorStatsResult
{
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int TotalCommits { get; set; }
    public int TotalAdditions { get; set; }
    public int TotalDeletions { get; set; }
}
