using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Google.GenAI;
using Google.GenAI.Types;
using DespensaInteligente.Application.Interfaces;
using DespensaInteligente.Application.Common.DTOs;
using DespensaInteligente.Application.Exceptions;
using DespensaInteligente.Application.Templates;
using DespensaInteligente.Infrastructure.Options;

namespace DespensaInteligente.Infrastructure.Services
{
    public class GeminiService : ILlmService
    {
        private readonly LlmOptions _options;
        private readonly ILogger<GeminiService> _logger;

        public GeminiService(
            IOptions<LlmOptions> options,
            ILogger<GeminiService> logger)
        {
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<InvoiceExtractionResult> ExtractInvoiceAsync(
            string prompt,
            Stream? file = null,
            string? contentType = null)
        {
            _logger.LogInformation("Iniciando extração de dados da Nota Fiscal com o provedor Gemini. Modelo: {Model}", _options.Model);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                string rawResponse = await GenerateAsync(prompt, file, contentType);
                stopwatch.Stop();
                
                _logger.LogInformation("Chamada ao Gemini realizada com sucesso em {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);

                var result = ParseLlmResponse(rawResponse);
                return result;
            }
            catch (LlmException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha na extração de dados da Nota Fiscal no provedor Gemini.");
                throw new InvalidLlmResponseException("Falha ao obter uma resposta válida do Gemini.", ex);
            }
        }

        public async Task<string> GenerateAsync(
            string prompt,
            Stream? file = null,
            string? contentType = null)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _logger.LogError("API Key do Gemini está vazia.");
                throw new InvalidApiKeyException("A API Key para o Gemini não foi configurada.");
            }

            try
            {
                var client = new Client(apiKey: _options.ApiKey);

                var contents = new List<Content>();
                var parts = new List<Part>();

                if (file != null)
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    
                    parts.Add(new Part
                    {
                        InlineData = new Blob
                        {
                            Data = ms.ToArray(),
                            MimeType = contentType ?? "image/jpeg"
                        }
                    });
                }

                parts.Add(new Part
                {
                    Text = prompt
                });

                contents.Add(new Content
                {
                    Role = "user",
                    Parts = parts
                });

                var config = new GenerateContentConfig
                {
                    SystemInstruction = new Content
                    {
                        Parts = new List<Part>
                        {
                            new Part { Text = InvoicePromptTemplate.SystemPrompt }
                        }
                    },
                    Temperature = 0.1,
                    ResponseMimeType = "application/json"
                };

                var response = await client.Models.GenerateContentAsync(
                    model: _options.Model,
                    contents: contents,
                    config: config
                );

                if (response?.Candidates == null || response.Candidates.Count == 0 ||
                    response.Candidates[0].Content?.Parts == null || response.Candidates[0].Content.Parts.Count == 0)
                {
                    throw new InvalidLlmResponseException("A resposta obtida do Gemini não contém candidatos ou partes.");
                }

                var textResult = response.Candidates[0].Content.Parts[0].Text;
                if (string.IsNullOrWhiteSpace(textResult))
                {
                    throw new InvalidLlmResponseException("O Gemini retornou um texto vazio.");
                }

                return textResult;
            }
            catch (Exception ex) when (IsTimeoutException(ex))
            {
                _logger.LogError(ex, "Timeout na chamada à API do Gemini.");
                throw new LlmTimeoutException("A requisição ao provedor Gemini expirou.", ex);
            }
            catch (Exception ex) when (IsApiKeyException(ex))
            {
                _logger.LogError(ex, "Chave de API do Gemini inválida ou não autorizada.");
                throw new InvalidApiKeyException("Chave de API do Gemini inválida.", ex);
            }
            catch (Exception ex) when (IsRateLimitException(ex))
            {
                _logger.LogError(ex, "Limite de requisições excedido no Gemini.");
                throw new RateLimitExceededException("Limite de requisições atingido para a API do Gemini.", ex);
            }
            catch (Exception ex) when (ex is LlmException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro de comunicação com a API do Gemini.");
                throw new InvalidLlmResponseException("Erro inesperado na chamada ao Gemini.", ex);
            }
        }

        private bool IsTimeoutException(Exception ex)
        {
            return ex is TimeoutException ||
                   ex is TaskCanceledException ||
                   ex is OperationCanceledException ||
                   ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsApiKeyException(Exception ex)
        {
            var msg = ex.ToString();
            return msg.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("API key not valid", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("401", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("Forbidden", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("403", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsRateLimitException(Exception ex)
        {
            var msg = ex.ToString();
            return msg.Contains("429", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("Quota exceeded", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("Rate limit", StringComparison.OrdinalIgnoreCase);
        }

        private InvoiceExtractionResult ParseLlmResponse(string rawJson)
        {
            try
            {
                // Remove wrappers de markdown se presentes
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
                throw new InvalidJsonException("Resposta do Gemini não contém um JSON estruturado válido.", ex);
            }
            catch (Exception ex) when (ex is not LlmException)
            {
                _logger.LogError(ex, "Erro inesperado ao tratar e normalizar dados do Gemini.");
                throw new InvalidLlmResponseException("Falha ao analisar os dados retornados pelo Gemini.", ex);
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
