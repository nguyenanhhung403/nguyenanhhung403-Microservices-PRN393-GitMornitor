using System;
using System.Collections.Generic;

namespace Classroom.API.DTOs;

public record CreateClassRoomDto(string Name);
public record UpdateClassRoomDto(string? Name, bool? IsActive);
public record ClassRoomResponseDto(int Id, string Name, int TeacherId, bool IsActive, int TotalGroups, int TotalStudents);

public record ImportStudentDto(string UserName, string RepositoryUrl);
public record UpdateStudentDto(string? Name, string? GitHubUsername, string? Email, bool? IsLeader);
public record StudentResponseDto(int Id, string StudentCode, string Name, string GitHubUsername, string? AvatarUrl, string? Email, bool IsLeader, string? GroupName, int GroupId);

public record TokenDto(string Token);
