using System;

namespace DespensaInteligente.Application.Common.DTOs
{
    public record RegisterRequestDto(string Email, string Password);
    public record LoginRequestDto(string Email, string Password);
    public record RefreshRequestDto(string RefreshToken);
    public record UserInfoDto(Guid Id, string Email);
    public record LoginResponseDto(string AccessToken, string RefreshToken, UserInfoDto User);
    public record RefreshResponseDto(string AccessToken, string RefreshToken);
}
