using System;
using System.Threading.Tasks;
using DespensaInteligente.Application.Common.DTOs;

namespace DespensaInteligente.Application.Common.Interfaces
{
    public interface IAuthService
    {
        Task<bool> RegisterAsync(RegisterRequestDto request);
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
        Task<RefreshResponseDto> RefreshAsync(RefreshRequestDto request);
        Task LogoutAsync(Guid userId);
    }
}
