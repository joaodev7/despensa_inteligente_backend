using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Application.Services;
using DespensaInteligente.Infrastructure.Data;
using DespensaInteligente.Infrastructure.Services;

namespace DespensaInteligente.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Bind interface and placeholder to concrete context
            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
            services.AddScoped<AppDbContextPlaceholder>(provider => provider.GetRequiredService<AppDbContext>());

            // Typed HttpClients
            services.AddHttpClient<INfeSefazService, NfeSefazService>();
            services.AddHttpClient<ILlmService, LlmService>();

            // Storage
            services.AddScoped<IFileStorageService, FileStorageService>();

            return services;
        }
    }
}
