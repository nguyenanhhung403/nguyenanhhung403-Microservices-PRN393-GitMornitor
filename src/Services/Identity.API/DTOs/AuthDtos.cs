namespace Identity.API.DTOs;

public record RegisterDto(string Username, string Name, string? Email, string Password);
public record LoginDto(string Username, string Password);
public record AuthResponseDto(string Token, int TeacherId, string Username, string Name, DateTime Expiration);
