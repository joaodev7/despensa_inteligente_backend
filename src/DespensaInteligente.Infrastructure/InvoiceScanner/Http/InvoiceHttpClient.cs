using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DespensaInteligente.Infrastructure.InvoiceScanner.Http;

public class InvoiceHttpClient : IInvoiceHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InvoiceHttpClient> _logger;

    public InvoiceHttpClient(HttpClient httpClient, ILogger<InvoiceHttpClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> DownloadHtmlAsync(string url, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Efetuando requisição HTTP para baixar a nota fiscal da URL: {Url}", url);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Servidor da SEFAZ respondeu com código de erro HTTP {StatusCode} para a URL {Url}", response.StatusCode, url);
                throw new HttpRequestException($"Servidor da SEFAZ retornou status {(int)response.StatusCode} ({response.StatusCode}).");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            
            // Tratar codificação do HTML (muitas páginas de SEFAZ utilizam ISO-8859-1 ou Windows-1252)
            var charSet = response.Content.Headers.ContentType?.CharSet?.ToLowerInvariant();
            Encoding encoding = charSet switch
            {
                "iso-8859-1" or "latin1" => Encoding.Latin1,
                "windows-1252" => Encoding.GetEncoding("Windows-1252"),
                _ => Encoding.UTF8
            };

            var html = encoding.GetString(bytes);

            if (string.IsNullOrWhiteSpace(html))
            {
                throw new HttpRequestException("O conteúdo HTML retornado pela SEFAZ está vazio.");
            }

            return html;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Timeout ocorrido ao tentar baixar o HTML da nota fiscal na URL: {Url}", url);
            throw new TimeoutException("O tempo de resposta do servidor da SEFAZ excedeu o limite máximo (Timeout).");
        }
    }
}
