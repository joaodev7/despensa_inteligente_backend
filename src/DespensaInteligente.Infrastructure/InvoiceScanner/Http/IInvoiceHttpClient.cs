namespace DespensaInteligente.Infrastructure.InvoiceScanner.Http;

/// <summary>
/// Cliente HTTP responsável pelo download das páginas das SEFAZ.
/// </summary>
public interface IInvoiceHttpClient
{
    /// <summary>
    /// Baixa o conteúdo HTML da nota fiscal a partir de uma URL.
    /// </summary>
    /// <param name="url">URL da NFC-e.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>String contendo o HTML retornado.</returns>
    Task<string> DownloadHtmlAsync(string url, CancellationToken cancellationToken);
}
