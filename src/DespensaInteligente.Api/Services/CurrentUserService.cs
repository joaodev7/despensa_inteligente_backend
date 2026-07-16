using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using DespensaInteligente.Application.Common.Interfaces;

namespace DespensaInteligente.Api.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? UserId
        {
            get
            {
                var userIdString = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                                   ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");

                return Guid.TryParse(userIdString, out var guid) ? guid : null;
            }
        }
    }
}
