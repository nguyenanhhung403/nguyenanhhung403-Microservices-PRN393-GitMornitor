namespace GitStudentMonitorApi.Models;

// Teacher
public record TeacherRequest(string Username, string Name, string Email);
public record TeacherUpdateRequest(string? Name, string? Email);

// Classroom
public record ClassroomRequest(string Name, int TeacherId);
public record ClassroomUpdateRequest(string? Name, bool? IsActive);
public record TokenRequest(string Token);

// Student
public record ImportStudentItem(string UserName, string RepositoryUrl);
public record StudentUpdateRequest(string? Name, string? GitHubUsername);
