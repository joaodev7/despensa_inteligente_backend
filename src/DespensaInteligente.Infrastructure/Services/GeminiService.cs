using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
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
        private readonly List<string> _modelsList = new();

        public GeminiService(
            IOptions<LlmOptions> options,
            ILogger<GeminiService> logger)
        {
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeModelsList();
        }

        private void InitializeModelsList()
        {
            var models = _options.Models;
            if (models != null && models.Count > 0)
            {
                foreach (var m in models)
                {
                    var clean = NormalizeModelName(m);
                    if (!string.IsNullOrEmpty(clean))
                    {
                        _modelsList.Add(clean);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(_options.Model))
            {
                var clean = NormalizeModelName(_options.Model);
                if (!string.IsNullOrEmpty(clean))
                {
                    _modelsList.Add(clean);
                }
            }

            if (_modelsList.Count == 0)
            {
                _modelsList.Add("gemini-3.5-flash");
            }
        }

        public async Task<InvoiceExtractionResult> ExtractInvoiceAsync(
            string prompt,
            Stream? file = null,
            string? contentType = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Iniciando extração de dados da Nota Fiscal no Gemini.");
            
            try
            {
                string rawResponse = await GenerateAsync(prompt, file, contentType, cancellationToken);
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
                throw new LlmCommunicationException("Falha ao obter uma resposta válida do Gemini após processamento.", ex);
            }
        }

        public async Task<string> GenerateAsync(
            string prompt,
            Stream? file = null,
            string? contentType = null,
            CancellationToken cancellationToken = default)
        {
            Exception? lastException = null;
            var stopwatchTotal = System.Diagnostics.Stopwatch.StartNew();

            for (int modelIndex = 0; modelIndex < _modelsList.Count; modelIndex++)
            {
                string currentModel = _modelsList[modelIndex];
                int maxAttempts = 3;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    _logger.LogInformation("Tentativa {Attempt} utilizando {Model}", attempt, currentModel);
                    var stopwatchAttempt = System.Diagnostics.Stopwatch.StartNew();

                    try
                    {
                        string result = await GenerateWithModelAsync(currentModel, prompt, file, contentType, cancellationToken);
                        stopwatchAttempt.Stop();
                        stopwatchTotal.Stop();

                        _logger.LogInformation("Resposta obtida com sucesso usando '{Model}' na tentativa {Attempt} em {ElapsedSeconds:F1} segundos. Tempo total da operação: {TotalSeconds:F1} segundos.",
                            currentModel,
                            attempt,
                            stopwatchAttempt.Elapsed.TotalSeconds,
                            stopwatchTotal.Elapsed.TotalSeconds);

                        return result;
                    }
                    catch (Exception rawEx)
                    {
                        stopwatchAttempt.Stop();
                        var translatedEx = TranslateException(rawEx, currentModel);
                        lastException = translatedEx;

                        _logger.LogWarning("Tentativa {Attempt} utilizando {Model} falhou. Motivo: {Reason}",
                            attempt, currentModel, translatedEx.Message);

                        cancellationToken.ThrowIfCancellationRequested();

                        // Se a API Key for inválida, não adianta tentar outros modelos ou retentativas
                        if (translatedEx is InvalidApiKeyException)
                        {
                            throw translatedEx;
                        }

                        if (attempt < maxAttempts && IsRetryableException(translatedEx))
                        {
                            int delaySeconds = (int)Math.Pow(2, attempt - 1); // Exponential backoff: 1s, 2s
                            _logger.LogInformation("Erro temporário detectado. Aguardando {Delay} segundos antes de tentar novamente...", delaySeconds);
                            
                            try
                            {
                                await Task.Delay(delaySeconds * 1000, cancellationToken);
                            }
                            catch (OperationCanceledException)
                            {
                                throw lastException;
                            }
                        }
                        else
                        {
                            // Esgotado ou erro não-retentável para este modelo
                            if (modelIndex < _modelsList.Count - 1)
                            {
                                _logger.LogWarning("Falha definitiva no modelo '{Model}'. Tentando próximo modelo de fallback.", currentModel);
                            }
                            break;
                        }
                    }
                }
            }

            throw lastException ?? new LlmException("Não foi possível gerar conteúdo com nenhum dos modelos configurados.");
        }

        private async Task<string> GenerateWithModelAsync(
            string model,
            string prompt,
            Stream? file = null,
            string? contentType = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw new InvalidApiKeyException("A API Key para o Gemini não foi configurada.");
            }

            var client = new Client(apiKey: _options.ApiKey);
            var contents = new List<Content>();
            var parts = new List<Part>();

            if (file != null)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, cancellationToken);
                
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
                model: model,
                contents: contents,
                config: config,
                cancellationToken: cancellationToken
            );

            if (response?.Candidates == null || response.Candidates.Count == 0 ||
                response.Candidates[0].Content?.Parts == null || response.Candidates[0].Content.Parts.Count == 0)
            {
                throw new InvalidLlmResponseException("A resposta obtida do Gemini está vazia ou incompleta.");
            }

            var textResult = response.Candidates[0].Content.Parts[0].Text;
            if (string.IsNullOrWhiteSpace(textResult))
            {
                throw new InvalidLlmResponseException("O texto retornado pelo modelo do Gemini está vazio.");
            }

            return textResult;
        }

        private string NormalizeModelName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var clean = name.Trim();
            if (clean.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring("models/".Length);
            }
            return clean;
        }

        private bool IsRetryableException(Exception ex)
        {
            return ex is LlmTimeoutException || 
                   ex is QuotaExceededException || 
                   ex is ModelUnavailableException || 
                   ex is HighDemandException;
        }

        private Exception TranslateException(Exception ex, string modelName)
        {
            if (ex is LlmException) return ex;

            var msg = ex.ToString();

            if (msg.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("API key not valid", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("401", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("Forbidden", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("403", StringComparison.OrdinalIgnoreCase))
            {
                return new InvalidApiKeyException($"Chave de API do Gemini inválida ou não autorizada.", ex);
            }

            if (msg.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("no longer available", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("not available", StringComparison.OrdinalIgnoreCase))
            {
                return new ModelNotFoundException($"Modelo '{modelName}' inexistente ou descontinuado no Gemini.", ex);
            }

            if (msg.Contains("429", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            {
                return new QuotaExceededException($"Quota de requisições excedida para o modelo '{modelName}'.", ex);
            }

            if (msg.Contains("503", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("service unavailable", StringComparison.OrdinalIgnoreCase))
            {
                return new ModelUnavailableException($"Modelo '{modelName}' está temporariamente indisponível no Gemini.", ex);
            }

            if (msg.Contains("high demand", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("overloaded", StringComparison.OrdinalIgnoreCase))
            {
                return new HighDemandException($"Modelo '{modelName}' está sob alta demanda ou sobrecarregado.", ex);
            }

            if (ex is TimeoutException ||
                ex is TaskCanceledException ||
                ex is OperationCanceledException ||
                msg.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                return new LlmTimeoutException($"A requisição para o modelo '{modelName}' no Gemini expirou.", ex);
            }

            return new LlmCommunicationException($"Falha de comunicação ou erro inesperado com o modelo '{modelName}' no Gemini.", ex);
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
