using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;
using DespensaInteligente.Application.Common.DTOs;
using DespensaInteligente.Infrastructure.Data;

namespace DespensaInteligente.Tests
{
    public class AuthIntegrationTests : IAsyncLifetime
    {
        // Setup PostgreSQL Testcontainer
        private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("despensa_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        private WebApplicationFactory<Program>? _factory;
        private HttpClient? _client;

        public async Task InitializeAsync()
        {
            // Start Docker container
            await _dbContainer.StartAsync();

            // Setup in-memory Test Web Server overriding Connection String to use Container
            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Development");
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        // Override database connection string to point to container
                        config.AddInMemoryCollection(new[]
                        {
                            new System.Collections.Generic.KeyValuePair<string, string?>(
                                "ConnectionStrings:Default", _dbContainer.GetConnectionString())
                        });
                    });
                });

            _client = _factory.CreateClient();

            // Run database migrations on test database
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            if (_client != null) _client.Dispose();
            if (_factory != null) await _factory.DisposeAsync();
            await _dbContainer.StopAsync();
        }

        [Fact]
        public async Task RegistrarELogar_DeveRetornarTokensValidos()
        {
            // Arrange
            var registerPayload = new RegisterRequestDto("testintegration@despensa.com", "senhaTeste123!");
            var loginPayload = new LoginRequestDto("testintegration@despensa.com", "senhaTeste123!");

            // Act: 1. Register User
            var registerResponse = await _client!.PostAsJsonAsync("/api/auth/register", registerPayload);
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            // Act: 2. Login User
            var loginResponse = await _client!.PostAsJsonAsync("/api/auth/login", loginPayload);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var loginData = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();

            // Assert
            Assert.NotNull(loginData);
            Assert.NotNull(loginData.AccessToken);
            Assert.NotNull(loginData.RefreshToken);
            Assert.Equal("testintegration@despensa.com", loginData.User.Email);
        }
    }
}
