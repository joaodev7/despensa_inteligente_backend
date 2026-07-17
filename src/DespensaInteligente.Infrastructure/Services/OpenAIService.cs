using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DespensaInteligente.Application.Interfaces;
using DespensaInteligente.Application.Common.DTOs;
using DespensaInteligente.Application.Exceptions;
using DespensaInteligente.Application.Templates;
using DespensaInteligente.Infrastructure.Options;

namespace DespensaInteligente.Infrastructure.Services
{
    public class OpenAIService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly LlmOptions _options;
        private readonly ILogger<OpenAIService> _logger;

        public OpenAIService(
            HttpClient httpClient,
            IOptions<LlmOptions> options,
            ILogger<OpenAIService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<InvoiceExtractionResult> ExtractInvoiceAsync(
            string prompt,
            Stream? file = null,
            string? contentType = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Iniciando extração de dados da Nota Fiscal com o provedor OpenAI. Modelo: {Model}", _options.Model);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string rawResponse = await GenerateAsync(prompt, file, contentType, cancellationToken);
                stopwatch.Stop();

                _logger.LogInformation("Chamada à OpenAI realizada com sucesso em {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);

                var result = ParseLlmResponse(rawResponse);
                return result;
            }
            catch (LlmException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha na extração de dados da Nota Fiscal no provedor OpenAI.");
                throw new InvalidLlmResponseException("Falha ao obter uma resposta válida da OpenAI.", ex);
            }
        }

        public async Task<string> GenerateAsync(
            string prompt,
            Stream? file = null,
            string? contentType = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _logger.LogError("API Key da OpenAI está vazia.");
                throw new InvalidApiKeyException("A API Key para a OpenAI não foi configurada.");
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

                var messages = new List<object>
                {
                    new { role = "system", content = InvoicePromptTemplate.SystemPrompt }
                };

                if (file != null)
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms, cancellationToken);
                    string base64Image = Convert.ToBase64String(ms.ToArray());

                    messages.Add(new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new { type = "image_url", image_url = new { url = $"data:{contentType ?? "image/jpeg"};base64,{base64Image}" } }
                        }
                    });
                }
                else
                {
                    messages.Add(new { role = "user", content = prompt });
                }

                var payload = new
                {
                    model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4o-mini" : _options.Model,
                    messages = messages.ToArray(),
                    temperature = 0.1,
                    response_format = new { type = "json_object" }
                };

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    int statusCode = (int)response.StatusCode;

                    _logger.LogError("Erro do provedor OpenAI: {StatusCode} - {ErrorContent}", statusCode, errorContent);

                    if (statusCode == 401 || statusCode == 403)
                    {
                        throw new InvalidApiKeyException($"API Key da OpenAI inválida ou não autorizada. Status: {statusCode}");
                    }
                    if (statusCode == 429)
                    {
                        throw new RateLimitExceededException("Limite de requisições excedido no provedor OpenAI.");
                    }
                    throw new InvalidLlmResponseException($"Provedor OpenAI retornou erro HTTP: {statusCode} - {errorContent}");
                }

                var responseData = await response.Content.ReadFromJsonAsync<OpenAiResponseDto>();
                if (responseData?.Choices == null || responseData.Choices.Count == 0 ||
                    string.IsNullOrWhiteSpace(responseData.Choices[0].Message?.Content))
                {
                    throw new InvalidLlmResponseException("A resposta obtida da OpenAI está vazia ou malformada.");
                }

                return responseData.Choices[0].Message.Content;
            }
            catch (Exception ex) when (IsTimeoutException(ex))
            {
                _logger.LogError(ex, "Timeout na chamada à API da OpenAI.");
                throw new LlmTimeoutException("A requisição ao provedor OpenAI expirou.", ex);
            }
            catch (LlmException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro de comunicação com a API da OpenAI.");
                throw new InvalidLlmResponseException("Erro inesperado na chamada à OpenAI.", ex);
            }
        }

        private bool IsTimeoutException(Exception ex)
        {
            return ex is TimeoutException ||
                   ex is TaskCanceledException ||
                   ex is OperationCanceledException ||
                   ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        }

        private InvoiceExtractionResult ParseLlmResponse(string rawJson)
        {
            try
            {
                string cleanJson = System.Text.RegularExpressions.Regex.Replace(rawJson, @"^```(?:json)?", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
                cleanJson = System.Text.RegularExpressions.Regex.Replace(cleanJson, @"```$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
                cleanJson = cleanJson.Trim();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var rawResult = JsonSerializer.Deserialize<RawLlmResult>(cleanJson, options);
                if (rawResult == null)
                {
                    throw new InvalidJsonException("O JSON retornado pela IA está vazio ou inválido.");
                }

                var result = new InvoiceExtractionResult
                {
                    Estabelecimento = rawResult.Estabelecimento ?? "Supermercado Desconhecido",
                    CNPJ = System.Text.RegularExpressions.Regex.Replace(rawResult.CNPJ ?? "", @"\D", ""),
                    DataCompra = DateOnly.TryParse(rawResult.DataCompra, out var parsedDate) ? parsedDate : DateOnly.FromDateTime(DateTime.Today),
                    ValorTotal = rawResult.ValorTotal,
                    ChaveAcesso = System.Text.RegularExpressions.Regex.Replace(rawResult.ChaveAcesso ?? "", @"\D", "")
                };

                if (rawResult.Itens != null)
                {
                    foreach (var item in rawResult.Itens)
                    {
                        result.Itens.Add(new InvoiceItemResult
                        {
                            Descricao = item.Descricao ?? "Produto Desconhecido",
                            Quantidade = item.Quantidade <= 0 ? 1 : item.Quantidade,
                            Unidade = NormalizarUnidade(item.Unidade),
                            ValorUnitario = item.ValorUnitario,
                            ValorTotal = item.ValorTotal > 0 ? item.ValorTotal : item.Quantidade * item.ValorUnitario
                        });
                    }
                }

                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro de desserialização do JSON obtido da IA.");
                throw new InvalidJsonException("Resposta da OpenAI não contém um JSON estruturado válido.", ex);
            }
            catch (Exception ex) when (ex is not LlmException)
            {
                _logger.LogError(ex, "Erro inesperado ao tratar e normalizar dados da OpenAI.");
                throw new InvalidLlmResponseException("Falha ao analisar os dados retornados pela OpenAI.", ex);
            }
        }

        private string NormalizarUnidade(string? unidade)
        {
            if (string.IsNullOrWhiteSpace(unidade)) return "un";
            string u = unidade.Trim().ToLowerInvariant();
            if (u == "un" || u == "und" || u == "unidade") return "un";
            if (u == "kg" || u == "kilo" || u == "quilo") return "kg";
            if (u == "g" || u == "gr" || u == "grama" || u == "gramas") return "g";
            if (u == "l" || u == "litro" || u == "litros") return "l";
            if (u == "ml" || u == "militro" || u == "mililitros") return "ml";
            if (u == "pct" || u == "pacote" || u == "pacotes") return "pct";
            return "un";
        }

        private class OpenAiResponseDto
        {
            public List<ChoiceDto>? Choices { get; set; }
        }

        private class ChoiceDto
        {
            public MessageDto? Message { get; set; }
        }

        private class MessageDto
        {
            public string? Content { get; set; }
        }

        private class RawLlmResult
        {
            public string? Estabelecimento { get; set; }
            public string? CNPJ { get; set; }
            public string? DataCompra { get; set; }
            public decimal ValorTotal { get; set; }
            public string? ChaveAcesso { get; set; }
            public List<RawLlmItem>? Itens { get; set; }
        }

        private class RawLlmItem
        {
            public string? Descricao { get; set; }
            public decimal Quantidade { get; set; }
            public string? Unidade { get; set; }
            public decimal ValorUnitario { get; set; }
            public decimal ValorTotal { get; set; }
        }
    }
}
