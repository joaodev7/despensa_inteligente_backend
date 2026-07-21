using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DespensaInteligente.Infrastructure.InvoiceScanner.Models;

namespace DespensaInteligente.Infrastructure.InvoiceScanner.Http;

/// <summary>
/// Cliente HTTP para consumo da API REST interna da SEFAZ-CE via protocolo HTTP.
/// A SEFAZ-CE responde aos endpoints internos (/nfce/api/notasFiscal/qrcodevX/) exclusivamente via HTTP.
/// </summary>
public class SefazCeApiClient : ISefazCeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;
    private readonly ILogger<SefazCeApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public SefazCeApiClient(
        HttpClient httpClient,
        CookieContainer cookieContainer,
        ILogger<SefazCeApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cookieContainer = cookieContainer ?? throw new ArgumentNullException(nameof(cookieContainer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SefazCeApiResponse> FetchInvoiceHtmlAsync(SefazCeQrCodePayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var baseUrl = GetApiBaseUrl(payload.TipoAmbiente);
        var baseUri = new Uri(baseUrl);
        var endpointUrl = BuildEndpointUrl(baseUrl, payload.VersaoQrCode);
        var ambienteDesc = payload.TipoAmbiente == 1 ? "Produção (1)" : $"Homologação ({payload.TipoAmbiente})";

        _logger.LogInformation("[SEFAZ-CE HTTP API] Iniciando consulta. Ambiente: {Ambiente}, BaseUrl: {BaseUrl}, Endpoint: {Endpoint}",
            ambienteDesc, baseUrl, endpointUrl);

        // 1. Estabelecer sessão e obter cookies preliminares de ShowNFCe.html
        await EnsureSessionCookiesAsync(baseUrl, baseUri, cancellationToken);

        // 2. Montar Payload JSON
        var requestPayloadObj = new
        {
            chave_acesso = payload.ChaveAcesso,
            versao_qrcode = payload.VersaoQrCode.ToString(),
            tipo_ambiente = payload.TipoAmbiente.ToString(),
            identificador_csc = payload.IdentificadorCSC,
            codigo_hash = payload.CodigoHash
        };

        var jsonPayload = JsonSerializer.Serialize(requestPayloadObj, JsonOptions);

        // 3. Montar Requisição HTTP POST com cabeçalhos oficiais
        using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Add("Accept", "application/json, text/plain, */*");
        request.Headers.Add("Accept-Language", "pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7");
        request.Headers.Add("Origin", baseUrl);
        request.Headers.Add("Referer", $"{baseUrl}/pages/ShowNFCe.html");
        
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        // Logging pré-requisição: URL final, payload e headers principais
        LogPreRequestDetails(endpointUrl, request, jsonPayload, baseUri);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var responseBody = Encoding.UTF8.GetString(responseBytes);

            // Logging pós-resposta: StatusCode, Tamanho do Conteúdo, Erro e Tamanho do XML
            LogPostResponseSummary(response, responseBody, responseBytes.Length, stopwatch.ElapsedMilliseconds);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[FALHA HTTP SEFAZ-CE] Status Code: {StatusCode} ({ReasonPhrase}). Body da resposta:\n{ResponseBody}",
                    (int)response.StatusCode, response.ReasonPhrase, responseBody);

                throw new HttpRequestException($"API SEFAZ-CE respondeu com código de erro HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). Conteúdo: {responseBody}");
            }

            var apiResponse = JsonSerializer.Deserialize<SefazCeApiResponse>(responseBody, JsonOptions);

            if (apiResponse == null)
            {
                _logger.LogError("[ERRO DESSERIALIZAÇÃO] Corpo JSON retornado pela SEFAZ-CE é nulo.");
                throw new InvalidOperationException("Resposta JSON nula ou malformada recebida da API da SEFAZ-CE.");
            }

            if (!string.IsNullOrWhiteSpace(apiResponse.Erro))
            {
                _logger.LogWarning("[ERRO SEFAZ-CE] Campo 'erro' preenchido na resposta JSON: {Erro}", apiResponse.Erro);
                throw new InvalidOperationException($"API SEFAZ-CE retornou mensagem de erro: {apiResponse.Erro}");
            }

            if (string.IsNullOrWhiteSpace(apiResponse.Xml))
            {
                _logger.LogError("[ERRO CAMPO XML] Campo 'xml' contendo o HTML da nota veio vazio na resposta JSON.");
                throw new InvalidOperationException("O campo 'xml' com o HTML da nota fiscal está vazio na resposta da SEFAZ-CE.");
            }

            _logger.LogInformation("[SUCESSO SEFAZ-CE] HTML extraído do campo 'xml' possui {Length} caracteres.", apiResponse.Xml.Length);

            return apiResponse;
        }
        catch (SocketException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[ERRO REDE/SOCKET] Falha de socket em {Endpoint}. ErrorCode: {ErrorCode}", endpointUrl, ex.SocketErrorCode);
            throw new HttpRequestException($"Falha de conexão de rede/socket ({ex.SocketErrorCode}) em {endpointUrl}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[TIMEOUT] Tempo limite excedido de {ElapsedMs}ms ao aguardar resposta de {Endpoint}", stopwatch.ElapsedMilliseconds, endpointUrl);
            throw new TimeoutException($"Tempo limite excedido ao comunicar com a SEFAZ-CE em {endpointUrl}.", ex);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[ERRO HTTP] Falha na requisição HTTP para {Endpoint}.", endpointUrl);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[EXCEÇÃO INESPERADA] Erro {Type} ao chamar {Endpoint}.", ex.GetType().Name, endpointUrl);
            throw;
        }
    }

    private static string GetApiBaseUrl(int ambiente)
    {
        // 1 = Produção (http://nfce.sefaz.ce.gov.br)
        // 2 = Homologação (http://nfceh.sefaz.ce.gov.br)
        return ambiente == 1
            ? "http://nfce.sefaz.ce.gov.br"
            : "http://nfceh.sefaz.ce.gov.br";
    }

    private static string BuildEndpointUrl(string baseUrl, int versaoQrCode)
    {
        var versionPath = versaoQrCode switch
        {
            1 => "qrcode",
            2 => "qrcodev2",
            3 => "qrcodev3",
            100 => "qrcodev100",
            _ => $"qrcodev{versaoQrCode}"
        };

        return $"{baseUrl}/nfce/api/notasFiscal/{versionPath}/";
    }

    private async Task EnsureSessionCookiesAsync(string baseUrl, Uri baseUri, CancellationToken cancellationToken)
    {
        var existingCookies = _cookieContainer.GetCookies(baseUri);
        if (existingCookies.Count > 0)
        {
            _logger.LogInformation("[SESSÃO COOKIES] Reutilizando {Count} cookies da sessão HTTP.", existingCookies.Count);
            return;
        }

        var shellUrl = $"{baseUrl}/pages/ShowNFCe.html";
        _logger.LogInformation("[SESSÃO COOKIES] Efetuando GET preliminar HTTP em {ShellUrl}...", shellUrl);

        try
        {
            using var shellRequest = new HttpRequestMessage(HttpMethod.Get, shellUrl);
            shellRequest.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            shellRequest.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            shellRequest.Headers.Add("Accept-Language", "pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7");

            using var shellResponse = await _httpClient.SendAsync(shellRequest, cancellationToken);
            var capturedCookies = _cookieContainer.GetCookies(baseUri);

            _logger.LogInformation("[SESSÃO COOKIES] GET concluído com status {StatusCode}. Total de cookies capturados: {Count}",
                (int)shellResponse.StatusCode, capturedCookies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SESSÃO COOKIES] GET em ShowNFCe.html falhou ({Message}). Prosseguindo com POST...", ex.Message);
        }
    }

    private void LogPreRequestDetails(string endpointUrl, HttpRequestMessage request, string jsonPayload, Uri baseUri)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== [PRÉ-REQUISIÇÃO HTTP SEFAZ-CE] ===");
        sb.AppendLine($"URL Final: {endpointUrl}");
        sb.AppendLine($"Método: {request.Method}");
        sb.AppendLine("Headers Principais:");
        foreach (var header in request.Headers)
        {
            sb.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
        }
        sb.AppendLine($"Payload JSON: {jsonPayload}");
        sb.AppendLine("======================================");

        _logger.LogInformation("{Details}", sb.ToString());
    }

    private void LogPostResponseSummary(HttpResponseMessage response, string responseBody, int contentLength, long elapsedMs)
    {
        string? erroField = null;
        int xmlCharCount = 0;

        try
        {
            var apiResp = JsonSerializer.Deserialize<SefazCeApiResponse>(responseBody, JsonOptions);
            if (apiResp != null)
            {
                erroField = apiResp.Erro;
                xmlCharCount = apiResp.Xml?.Length ?? 0;
            }
        }
        catch
        {
            // Ignora erro de parse secundário no log
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== [RESPOSTA HTTP SEFAZ-CE] ===");
        sb.AppendLine($"Status Code: {(int)response.StatusCode} ({response.ReasonPhrase})");
        sb.AppendLine($"Tamanho do Conteúdo Retornado: {contentLength} bytes");
        sb.AppendLine($"Tempo Decorrido: {elapsedMs}ms");
        sb.AppendLine($"Campo 'erro': {(string.IsNullOrWhiteSpace(erroField) ? "(Nenhum/Vazio)" : erroField)}");
        sb.AppendLine($"Quantidade de Caracteres do Campo 'xml': {xmlCharCount}");
        sb.AppendLine("================================");

        _logger.LogInformation("{Summary}", sb.ToString());
    }
}
