using System;
using System.Collections.Generic;
using Classroom.API.Enums;

namespace Classroom.API.Entities;

public class ClassRoom
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<StudentGroup> StudentGroups { get; set; } = new List<StudentGroup>();
}

public class StudentGroup
{
    public int Id { get; set; }
    public int ClassRoomId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public string? Token { get; set; }
    public GroupStatus Status { get; set; } = GroupStatus.Active;
    public string? LastErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ClassRoom ClassRoom { get; set; } = null!;
    public ICollection<Student> Students { get; set; } = new List<Student>();
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

    public StudentGroup Group { get; set; } = null!;
}
