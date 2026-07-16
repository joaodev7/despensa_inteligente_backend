using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DespensaInteligente.Application.Common.DTOs;
using DespensaInteligente.Application.Common.Interfaces;

namespace DespensaInteligente.Infrastructure.Services
{
    public class LlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<LlmService> _logger;
        private readonly IConfiguration _configuration;

        public LlmService(
            HttpClient httpClient,
            ILogger<LlmService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<NfeExtractionResultDto> ExtractNfeDataAsync(string rawContent)
        {
            string provider = _configuration["Llm:Provider"] ?? "Ollama"; // OpenAI, Azure, Ollama
            string apiKey = _configuration["Llm:ApiKey"] ?? "";
            string model = _configuration["Llm:Model"] ?? (provider == "OpenAI" ? "gpt-4o-mini" : "llama3");
            string endpoint = _configuration["Llm:Endpoint"] ?? "";

            _logger.LogInformation("Iniciando extração de dados via LLM usando provedor {Provider}", provider);

            string systemPrompt = @"Você é um assistente especializado em extrair dados estruturados de Notas Fiscais de Consumidor Eletrônicas (NFC-e) a partir de códigos XML ou HTML.
Sua tarefa é analisar o conteúdo fornecido e retornar estritamente um objeto JSON contendo as informações extraídas.
O JSON resultante deve seguir exatamente o seguinte formato (com chaves em minúsculas e snake_case):
{
  ""mercado"": ""Nome do supermercado ou estabelecimento"",
  ""data_compra"": ""YYYY-MM-DD"",
  ""chave_acesso"": ""Chave de acesso de 44 dígitos numéricos"",
  ""valor_total"": 125.50,
  ""itens"": [
    {
      ""nome"": ""NOME ORIGINAL DO PRODUTO (abreviado)"",
      ""quantidade"": 2.0,
      ""unidade"": ""un|kg|g|l|ml|pct"",
      ""preco_unitario"": 10.50,
      ""preco_total"": 21.00
    }
  ]
}

Regras:
1. Extraia o nome do mercado/supermercado.
2. Identifique a data de compra e formate como YYYY-MM-DD.
3. Obtenha a chave de acesso de 44 dígitos (remova espaços).
4. O campo 'unidade' deve ser normalizado para uma das opções: un, kg, g, l, ml, pct.
5. Remova qualquer formatação markdown. Retorne APENAS o JSON puro. Não explique nada, não inclua blocos de código tipo ```json.";

            string userPrompt = $"Aqui está o conteúdo bruto do XML/HTML da NFC-e:\n\n{rawContent}";

            var request = new HttpRequestMessage(HttpMethod.Post, "");
            string requestJson = "";

            if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                string url = string.IsNullOrWhiteSpace(endpoint) ? "https://api.openai.com/v1/chat/completions" : endpoint;
                request.RequestUri = new Uri(url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var payload = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.1,
                    response_format = new { type = "json_object" }
                };
                requestJson = JsonSerializer.Serialize(payload);
            }
            else if (provider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
            {
                // Azure endpoint format: https://{resource}.openai.azure.com/openai/deployments/{deployment-id}/chat/completions?api-version=2023-05-15
                request.RequestUri = new Uri(endpoint);
                request.Headers.Add("api-key", apiKey);

                var payload = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.1
                };
                requestJson = JsonSerializer.Serialize(payload);
            }
            else // Ollama
            {
                string url = string.IsNullOrWhiteSpace(endpoint) ? "http://localhost:11434/v1/chat/completions" : endpoint;
                request.RequestUri = new Uri(url);

                var payload = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.1,
                    stream = false
                };
                requestJson = JsonSerializer.Serialize(payload);
            }

            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Erro do provedor LLM: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                throw new Exception($"Provedor LLM retornou erro: {response.StatusCode} - {errorContent}");
            }

            var responseData = await response.Content.ReadFromJsonAsync<LlmResponseDto>();
            if (responseData == null || responseData.Choices == null || responseData.Choices.Count == 0)
            {
                throw new Exception("Nenhuma resposta obtida do LLM.");
            }

            string contentText = responseData.Choices[0].Message.Content;
            return ParseLlmResponse(contentText);
        }

        private NfeExtractionResultDto ParseLlmResponse(string rawJson)
        {
            try
            {
                // Strip markdown code block wrappers if present
                string cleanJson = Regex.Replace(rawJson, @"^```(?:json)?", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                cleanJson = Regex.Replace(cleanJson, @"```$", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                cleanJson = cleanJson.Trim();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Map lowercase snake_case JSON from LLM to DTO properties
                var rawResult = JsonSerializer.Deserialize<RawLlmResultDto>(cleanJson, options);
                if (rawResult == null)
                {
                    throw new Exception("Falha ao desserializar JSON da resposta do LLM.");
                }

                var dto = new NfeExtractionResultDto
                {
                    Mercado = rawResult.Mercado ?? "Mercado Desconhecido",
                    DataCompra = DateOnly.TryParse(rawResult.DataCompra, out var parsedDate) ? parsedDate : DateOnly.FromDateTime(DateTime.Today),
                    ChaveAcesso = Regex.Replace(rawResult.ChaveAcesso ?? "", @"\D", ""),
                    ValorTotal = rawResult.ValorTotal
                };

                if (rawResult.Itens != null)
                {
                    foreach (var item in rawResult.Itens)
                    {
                        dto.Itens.Add(new NfeExtractionItemDto
                        {
                            Nome = item.Nome ?? "Produto Desconhecido",
                            Quantidade = item.Quantidade,
                            Unidade = NormalizarUnidade(item.Unidade),
                            PrecoUnitario = item.PrecoUnitario,
                            PrecoTotal = item.PrecoTotal > 0 ? item.PrecoTotal : item.Quantidade * item.PrecoUnitario
                        });
                    }
                }

                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar o JSON gerado pelo LLM. JSON retornado: {RawJson}", rawJson);
                throw new Exception($"Falha ao analisar os dados extraídos pelo LLM: {ex.Message}. Resposta bruta: {rawJson}", ex);
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

        // Inner classes for parsing response
        private class LlmResponseDto
        {
            public List<ChoiceDto> Choices { get; set; } = new();
        }

        private class ChoiceDto
        {
            public MessageDto Message { get; set; } = new();
        }

        private class MessageDto
        {
            public string Content { get; set; } = string.Empty;
        }

        private class RawLlmResultDto
        {
            public string? Mercado { get; set; }
            public string? DataCompra { get; set; }
            public string? ChaveAcesso { get; set; }
            public decimal ValorTotal { get; set; }
            public List<RawLlmItemDto>? Itens { get; set; }
        }

        private class RawLlmItemDto
        {
            public string? Nome { get; set; }
            public decimal Quantidade { get; set; }
            public string? Unidade { get; set; }
            public decimal PrecoUnitario { get; set; }
            public decimal PrecoTotal { get; set; }
        }
    }
}
