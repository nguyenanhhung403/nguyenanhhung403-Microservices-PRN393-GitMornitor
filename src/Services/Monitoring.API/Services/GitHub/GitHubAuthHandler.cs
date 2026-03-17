using System.Net.Http.Headers;

namespace Monitoring.API.Services.GitHub;

public class GitHubTokenProvider
{
    public string? CurrentToken { get; set; }
}

public class GitHubAuthHandler : DelegatingHandler
{
    private readonly GitHubTokenProvider _tokenProvider;

    public GitHubAuthHandler(GitHubTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_tokenProvider.CurrentToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenProvider.CurrentToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
