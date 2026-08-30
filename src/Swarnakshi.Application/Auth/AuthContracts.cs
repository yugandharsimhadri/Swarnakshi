using System.ComponentModel.DataAnnotations;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Auth;

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public record AuthUserDto(Guid Id, string Name, string Email, UserRole Role, IReadOnlyCollection<string> Permissions);

public record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthUserDto User);
