using System;
using System.Collections.Generic;

namespace Monitoring.API.DTOs;

public record DashboardResponseDto(string ClassRoom, List<LeaderboardItemDto> Leaderboard, List<RepositoryItemDto> Repositories);
public record LeaderboardItemDto(string Name, string GitHubUsername, string? AvatarUrl, string StudentCode, string? GroupName, int CommitCount, int LinesAdded, int LinesDeleted, DateTime? LastSync);
public record RepositoryItemDto(string GroupName, string RepositoryUrl, string Status, string? LastErrorMessage, object? Contributors);
