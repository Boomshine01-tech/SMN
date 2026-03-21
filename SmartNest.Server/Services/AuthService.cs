using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartNest.Server.Data;
using SmartNest.Shared.DTOs;
using SmartNest.Shared.Models;

namespace SmartNest.Server.Services;

public interface IAuthService
{
    Task<LoginResponse> RegisterAsync(RegisterRequest request);
    Task<LoginResponse> LoginAsync(LoginRequest request);
    string GenerateToken(User user, bool rememberMe = false);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext   _db;
    private readonly IConfiguration _cfg;

    public AuthService(AppDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest r)
    {
        if (await _db.Users.AnyAsync(u => u.Username == r.Username))
            return Fail("Ce nom d'utilisateur est déjà pris.");
        if (await _db.Users.AnyAsync(u => u.Email == r.Email))
            return Fail("Cet email est déjà utilisé.");

        var user = new User
        {
            Username     = r.Username,
            Email        = r.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(r.Password, 12),
            Role         = "User",
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.NotificationSettings.Add(new NotificationSetting { UserId = user.Id });
        await _db.SaveChangesAsync();

        return BuildResponse(user, false);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest r)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == r.Username && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(r.Password, user.PasswordHash))
            return Fail("Identifiants invalides.");

        return BuildResponse(user, r.RememberMe);
    }

    public string GenerateToken(User user, bool rememberMe = false)
    {
        var secret  = _cfg["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret manquant.");
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var hours   = rememberMe ? 720 : 168;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier,   user.Id.ToString()),
            new Claim(ClaimTypes.Name,             user.Username),
            new Claim(ClaimTypes.Email,            user.Email),
            new Claim(ClaimTypes.Role,             user.Role),
        };

        var token = new JwtSecurityToken(
            issuer:            _cfg["Jwt:Issuer"] ?? "SmartNest",
            audience:          _cfg["Jwt:Audience"] ?? "SmartNestClient",
            claims:            claims,
            notBefore:         DateTime.UtcNow,
            expires:           DateTime.UtcNow.AddHours(hours),
            signingCredentials:creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private LoginResponse BuildResponse(User u, bool rememberMe)
    {
        var token   = GenerateToken(u, rememberMe);
        var hours   = rememberMe ? 720 : 168;
        return new LoginResponse
        {
            Success    = true,
            Token      = token,
            Username   = u.Username,
            Email      = u.Email,
            Role       = u.Role,
            RememberMe = rememberMe,
            ExpiresAt  = DateTime.UtcNow.AddHours(hours)
        };
    }

    private static LoginResponse Fail(string msg) => new() { Success = false, Message = msg };
}
