using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Api.Data;
using TaskManager.Api.Models;

namespace TaskManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    private readonly PasswordHasher<AppUser> passwordHasher = new();

    public record LoginRequest(string Login, string Password);

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var login = request.Login.Trim();
        var user = await db.Users.FirstOrDefaultAsync(
            x => x.Username.ToLower() == login.ToLower() || x.Email == login.ToLower(), ct);

        if (user is null)
            return Unauthorized(new { message = "Неверный логин или пароль." });

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Неверный логин или пароль." });

        return Ok(BuildAuthResponse(user));
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Требуется авторизация." });

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
        return user is null
            ? Unauthorized(new { message = "Пользователь не найден." })
            : Ok(ToUserResponse(user));
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }

    private AuthResponse BuildAuthResponse(AppUser user)
    {
        var jwt = configuration.GetSection("Jwt");
        var key = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key не настроен.");
        var issuer = jwt["Issuer"] ?? "TaskManager.Api";
        var audience = jwt["Audience"] ?? "TaskManager.Frontend";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            ToUserResponse(user));
    }

    private static UserResponse ToUserResponse(AppUser user) =>
        new(user.Id, user.Username, user.Email, user.CreatedAtUtc);

    public record UserResponse(int Id, string Username, string Email, DateTime CreatedAtUtc);
    public record AuthResponse(string Token, UserResponse User);
}
