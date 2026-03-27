using System;
using System.Collections.Generic;

namespace GitStudentMonitorApi.Models;

public partial class SyncHistory
{
    public int Id { get; set; }

    public string BatchId { get; set; } = null!;

    public int StudentId { get; set; }

    public DateTime SyncTime { get; set; }

    public int CommitCount { get; set; }

    public int PullRequestCount { get; set; }

    public int IssuesCount { get; set; }

    public int LinesAdded { get; set; }

    public int LinesDeleted { get; set; }

    public DateTime? LastCommitDate { get; set; }

    public string? RawDataJson { get; set; }

    public virtual Student Student { get; set; } = null!;
}
