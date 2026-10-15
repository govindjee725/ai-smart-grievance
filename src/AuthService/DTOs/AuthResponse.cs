namespace AuthService.DTOs;

public record UserResponse(
    string Id,
    string Name,
    string Email,
    string Role
);

public record AuthResponse(
    string Token,
    UserResponse User
);