using System;
using System.Collections.Generic;

namespace GitStudentMonitorApi.Models;

public partial class Teacher
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Email { get; set; }

    public string? DefaultGitHubToken { get; set; }

    public DateTime? LastLogin { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<ClassRoom> ClassRooms { get; set; } = new List<ClassRoom>();
}
