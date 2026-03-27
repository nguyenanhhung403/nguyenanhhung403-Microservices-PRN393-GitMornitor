using System;
using System.Collections.Generic;

namespace GitStudentMonitorApi.Models;

public partial class ClassRoom
{
    public int Id { get; set; }

    public int TeacherId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<StudentGroup> StudentGroups { get; set; } = new List<StudentGroup>();

    public virtual Teacher Teacher { get; set; } = null!;
}
