namespace Identity.API.DTOs;

public record CreateTeacherDto(string Username, string Name, string? Email);
public record UpdateTeacherDto(string? Name, string? Email);
public record TeacherResponseDto(int Id, string Username, string Name, string? Email, DateTime? LastLogin);
