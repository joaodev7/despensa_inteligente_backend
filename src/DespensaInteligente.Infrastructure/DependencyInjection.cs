using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Application.Interfaces;
using DespensaInteligente.Application.Services;
using DespensaInteligente.Infrastructure.Data;
using DespensaInteligente.Infrastructure.Services;
using DespensaInteligente.Infrastructure.Options;

namespace DespensaInteligente.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                                   ?? configuration.GetConnectionString("Default");

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Bind interface and placeholder to concrete context
            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
            services.AddScoped<AppDbContextPlaceholder>(provider => provider.GetRequiredService<AppDbContext>());

            // Configure LLM Options
            services.Configure<LlmOptions>(configuration.GetSection("Llm"));

            // Register specific implementations
            services.AddTransient<GeminiService>();
            services.AddHttpClient<OpenAIService>();
            services.AddHttpClient<OllamaService>();

            // Factory registration for ILlmService based on Provider configuration
            services.AddScoped<ILlmService>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
                var provider = options.Provider?.Trim().ToLowerInvariant() ?? "gemini";

                return provider switch
                {
                    "gemini" => sp.GetRequiredService<GeminiService>(),
                    "openai" => sp.GetRequiredService<OpenAIService>(),
                    "ollama" => sp.GetRequiredService<OllamaService>(),
                    _ => throw new ArgumentException($"Provedor LLM inválido ou não suportado: {options.Provider}")
                };
            });

            // Typed HttpClients
            services.AddHttpClient<INfeSefazService, NfeSefazService>();

            // Storage
            services.AddScoped<IFileStorageService, FileStorageService>();

            return services;
        }
    }
}
