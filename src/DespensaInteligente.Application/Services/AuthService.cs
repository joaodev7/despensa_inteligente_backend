using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using DespensaInteligente.Application.Common.DTOs;
using DespensaInteligente.Application.Common.Interfaces;

namespace DespensaInteligente.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<IdentityUser<Guid>> _userManager;
        private readonly IConfiguration _configuration;
        private readonly DbContext _context;

        public AuthService(
            UserManager<IdentityUser<Guid>> userManager,
            IConfiguration configuration,
            AppDbContextPlaceholder context) // Will be mapped to AppDbContext in Infrastructure
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = (DbContext)context;
        }

        public async Task<bool> RegisterAsync(RegisterRequestDto request)
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                throw new Exception("Email já está cadastrado.");
            }

            var user = new IdentityUser<Guid>
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                UserName = request.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", System.Linq.Enumerable.Select(result.Errors, e => e.Description));
                throw new Exception($"Erro ao criar usuário: {errors}");
            }

            return true;
        }

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            {
                throw new Exception("Email ou senha inválidos.");
            }

            string accessToken = GenerateAccessToken(user);
            string refreshToken = GenerateRefreshToken();

            // Store refresh token
            await _userManager.SetAuthenticationTokenAsync(user, "DespensaInteligente", "RefreshToken", refreshToken);

            return new LoginResponseDto(
                accessToken,
                refreshToken,
                new UserInfoDto(user.Id, user.Email!)
            );
        }

        public async Task<RefreshResponseDto> RefreshAsync(RefreshRequestDto request)
        {
            // Find the user token entry
            var tokenEntry = await _context.Set<IdentityUserToken<Guid>>()
                .FirstOrDefaultAsync(t => t.LoginProvider == "DespensaInteligente" && t.Name == "RefreshToken" && t.Value == request.RefreshToken);

            if (tokenEntry == null)
            {
                throw new Exception("Token de atualização inválido ou expirado.");
            }

            var user = await _userManager.FindByIdAsync(tokenEntry.UserId.ToString());
            if (user == null)
            {
                throw new Exception("Usuário não encontrado.");
            }

            string newAccessToken = GenerateAccessToken(user);
            string newRefreshToken = GenerateRefreshToken();

            // Rotate refresh token
            await _userManager.RemoveAuthenticationTokenAsync(user, "DespensaInteligente", "RefreshToken");
            await _userManager.SetAuthenticationTokenAsync(user, "DespensaInteligente", "RefreshToken", newRefreshToken);

            return new RefreshResponseDto(newAccessToken, newRefreshToken);
        }

        public async Task LogoutAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user != null)
            {
                await _userManager.RemoveAuthenticationTokenAsync(user, "DespensaInteligente", "RefreshToken");
            }
        }

        private string GenerateAccessToken(IdentityUser<Guid> user)
        {
            var secretKey = _configuration["Jwt:Key"] ?? "chave-secreta-padrao-despensa-inteligente-2026-suficientemente-longa";
            var issuer = _configuration["Jwt:Issuer"] ?? "DespensaInteligenteApi";
            var audience = _configuration["Jwt:Audience"] ?? "DespensaInteligenteApp";
            var expiryMinutes = double.TryParse(_configuration["Jwt:ExpiryMinutes"], out var exp) ? exp : 60;

            var securityKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(securityKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }

    // Interface placeholder to break dependency cycle during application setup
    public interface AppDbContextPlaceholder
    {
        DbSet<TEntity> Set<TEntity>() where TEntity : class;
        Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default);
    }
}
