using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Application.Interfaces;
using DespensaInteligente.Application.Services;
using DespensaInteligente.Application.InvoiceScanner.Interfaces;
using DespensaInteligente.Infrastructure.Data;
using DespensaInteligente.Infrastructure.Services;
using DespensaInteligente.Infrastructure.Options;
using DespensaInteligente.Infrastructure.InvoiceScanner.Http;
using DespensaInteligente.Infrastructure.InvoiceScanner.Parsers;
using DespensaInteligente.Infrastructure.InvoiceScanner.Providers;

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

            // Support semicolon or comma-separated string for models in environment variables (e.g. Llm__Models="gemini-3.5-flash;gemini-3.1-flash-lite")
            services.PostConfigure<LlmOptions>(options =>
            {
                var rawModelsValue = configuration["Llm:Models"];
                if (!string.IsNullOrWhiteSpace(rawModelsValue) && (options.Models == null || options.Models.Count == 0))
                {
                    var splitModels = rawModelsValue.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    options.Models = new List<string>();
                    foreach (var modelName in splitModels)
                    {
                        options.Models.Add(modelName.Trim());
                    }
                }
            });

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

            // Module: InvoiceScanner Infrastructure Registrations
            services.AddSingleton<CookieContainer>();
            services.AddTransient<ISefazCeQrCodeParser, SefazCeQrCodeParser>();
            services.AddTransient<ISefazCeHtmlParser, SefazCeHtmlParser>();
            services.AddScoped<IInvoiceProvider, SefazCeProvider>();

            services.AddHttpClient<IInvoiceHttpClient, InvoiceHttpClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddTransientHttpErrorPolicy(policy => 
                policy.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

            services.AddHttpClient<ISefazCeApiClient, SefazCeApiClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<SefazCeApiClient>>();
                var cookieContainer = sp.GetRequiredService<CookieContainer>();

                var handler = new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                    AllowAutoRedirect = true,
                    UseCookies = true,
                    CookieContainer = cookieContainer,
                    SslOptions = new SslClientAuthenticationOptions
                    {
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                        RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                        {
                            if (sslPolicyErrors != SslPolicyErrors.None)
                            {
                                logger.LogWarning("[SSL/TLS AVISO] Falha na validação do certificado SSL da SEFAZ. Erros: {SslPolicyErrors}, Subject: {Subject}, Issuer: {Issuer}",
                                    sslPolicyErrors, certificate?.Subject, certificate?.Issuer);
                            }
                            else
                            {
                                logger.LogDebug("[SSL/TLS SUCESSO] Handshake TLS concluído com sucesso. Subject: {Subject}, Issuer: {Issuer}",
                                    certificate?.Subject, certificate?.Issuer);
                            }
                            return sslPolicyErrors == SslPolicyErrors.None;
                        }
                    }
                };
                return handler;
            })
            .AddTransientHttpErrorPolicy(policy => 
                policy.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

            return services;
        }
    }
}
