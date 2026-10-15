using System.Security.Claims;
using AuthService.DTOs;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService.Services.AuthService _auth;

    public AuthController(
        AuthService.Services.AuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        RegisterRequest request)
    {
        var result = await _auth.RegisterAsync(request);

        if (result is null)
        {
            return Conflict(new
            {
                message = "Email is already registered."
            });
        }

        return StatusCode(201, result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);

        if (result is null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var id = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(id))
            return Unauthorized();

        var user = await _auth.GetUserAsync(id);

        return user is null
            ? NotFound()
            : Ok(user);
    }
}