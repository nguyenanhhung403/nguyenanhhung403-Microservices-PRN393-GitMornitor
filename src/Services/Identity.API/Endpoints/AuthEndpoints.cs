using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Identity.API.Data;
using Identity.API.DTOs;
using Identity.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Identity.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/register", async (RegisterDto dto, IdentityDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return Results.BadRequest("Username and password are required.");

            if (dto.Password.Length < 6)
                return Results.BadRequest("Password must be at least 6 characters.");

            if (await db.Teachers.AnyAsync(t => t.Username == dto.Username))
                return Results.Conflict("Username already exists.");

            var teacher = new Teacher
            {
                Username = dto.Username,
                Name = dto.Name,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                LastLogin = DateTime.UtcNow
            };

            db.Teachers.Add(teacher);
            await db.SaveChangesAsync();

            return Results.Created($"/api/teachers/{teacher.Id}", new { teacher.Id, Message = "Registration successful." });
        }).WithName("Register");

        group.MapPost("/login", async (LoginDto dto, IdentityDbContext db, IConfiguration config) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return Results.BadRequest("Username and password are required.");

            var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Username == dto.Username);
            if (teacher == null)
                return Results.Unauthorized();

            if (string.IsNullOrEmpty(teacher.PasswordHash) || !BCrypt.Net.BCrypt.Verify(dto.Password, teacher.PasswordHash))
                return Results.Unauthorized();

            teacher.LastLogin = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var expiration = DateTime.UtcNow.AddMinutes(double.Parse(config["Jwt:ExpiresInMinutes"]!));
            var token = GenerateJwtToken(teacher, config, expiration);

            return Results.Ok(new AuthResponseDto(token, teacher.Id, teacher.Username, teacher.Name, expiration));
        }).WithName("Login");
    }

    private static string GenerateJwtToken(Teacher teacher, IConfiguration config, DateTime expiration)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, teacher.Id.ToString()),
            new Claim(ClaimTypes.Name, teacher.Username),
            new Claim("TeacherId", teacher.Id.ToString()),
            new Claim("Name", teacher.Name)
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expiration,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
