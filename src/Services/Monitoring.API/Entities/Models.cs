using System;
using System.Collections.Generic;
using Monitoring.API.Enums;

namespace Monitoring.API.Entities;

public class SyncHistory
{
    public int Id { get; set; }
    public string BatchId { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public DateTime SyncTime { get; set; } = DateTime.UtcNow;
    public int CommitCount { get; set; }
    public int PullRequestCount { get; set; }
    public int IssuesCount { get; set; }
    public int LinesAdded { get; set; }
    public int LinesDeleted { get; set; }
    public DateTime? LastCommitDate { get; set; }
    public string? RawDataJson { get; set; }
}

public class ClassRoom
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<StudentGroup> StudentGroups { get; set; } = new();
}

public class StudentGroup
{
    public int Id { get; set; }
    public int ClassRoomId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public string? Token { get; set; }
    public GroupStatus Status { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTime? LastSyncPushedAt { get; set; }
    public ClassRoom? ClassRoom { get; set; }
    public List<Student> Students { get; set; } = new();
}

public class Student
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string GitHubUsername { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Email { get; set; }
    public bool IsLeader { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
