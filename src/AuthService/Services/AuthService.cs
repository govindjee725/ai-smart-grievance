using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Services;

public class AuthService
{
    private readonly UserRepository _repository;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserRepository repository,
        IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    public async Task<AuthResponse?> RegisterAsync(
        RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _repository.FindByEmailAsync(email) is not null)
            return null;

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                request.Password, 12),
            Role = "citizen"
        };

        await _repository.CreateAsync(user);

        return CreateResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(
        LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _repository.FindByEmailAsync(email);

        if (user is null ||
            !BCrypt.Net.BCrypt.Verify(
                request.Password, user.PasswordHash))
        {
            return null;
        }

        return CreateResponse(user);
    }

    public async Task<UserResponse?> GetUserAsync(string id)
    {
        var user = await _repository.FindByIdAsync(id);

        return user is null ? null : ToResponse(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var secret = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException(
                "JWT secret is missing.");

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secret));

        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id!),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials
        );

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            ToResponse(user)
        );
    }

    private static UserResponse ToResponse(User user)
    {
        return new UserResponse(
            user.Id!,
            user.Name,
            user.Email,
            user.Role
        );
    }
}