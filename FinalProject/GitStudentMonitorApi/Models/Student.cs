using System;
using System.Collections.Generic;

namespace GitStudentMonitorApi.Models;

public partial class Student
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public string StudentCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string GitHubUsername { get; set; } = null!;

    public string? AvatarUrl { get; set; }

    public string? Email { get; set; }

    public bool IsLeader { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual StudentGroup Group { get; set; } = null!;

    public virtual ICollection<SyncHistory> SyncHistories { get; set; } = new List<SyncHistory>();
}
