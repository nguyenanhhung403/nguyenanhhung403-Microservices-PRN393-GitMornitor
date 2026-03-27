using System.Net.Http.Headers;

namespace GitStudentMonitorApi.Services;

public class GitHubTokenProvider
{
    public string? Token { get; set; }
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
        if (!string.IsNullOrEmpty(_tokenProvider.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenProvider.Token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
