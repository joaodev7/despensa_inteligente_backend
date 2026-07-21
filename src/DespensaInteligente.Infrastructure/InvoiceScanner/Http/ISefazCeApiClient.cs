using DespensaInteligente.Infrastructure.InvoiceScanner.Models;

namespace DespensaInteligente.Infrastructure.InvoiceScanner.Http;

/// <summary>
/// Cliente especializado para consumir a API REST interna da SEFAZ-CE (/nfce/api/notasFiscal/qrcodevX/).
/// </summary>
public interface ISefazCeApiClient
{
    /// <summary>
    /// Envia o payload extraído do QRCode para a API da SEFAZ-CE e obtém a resposta contendo o HTML/XML real da nota fiscal.
    /// </summary>
    /// <param name="payload">Payload estruturado com chave de acesso, versão e hash.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>SefazCeApiResponse com o HTML contido na propriedade Xml.</returns>
    Task<SefazCeApiResponse> FetchInvoiceHtmlAsync(SefazCeQrCodePayload payload, CancellationToken cancellationToken);
}
