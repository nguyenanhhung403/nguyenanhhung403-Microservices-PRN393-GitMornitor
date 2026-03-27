using System;
using System.Collections.Generic;

namespace GitStudentMonitorApi.Models;

public partial class StudentGroup
{
    public int Id { get; set; }

    public int ClassRoomId { get; set; }

    public string GroupName { get; set; } = null!;

    public string RepositoryUrl { get; set; } = null!;

    public string? Token { get; set; }

    public int? Status { get; set; }

    public string? LastErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ClassRoom ClassRoom { get; set; } = null!;

    public virtual ICollection<Student> Students { get; set; } = new List<Student>();
}
