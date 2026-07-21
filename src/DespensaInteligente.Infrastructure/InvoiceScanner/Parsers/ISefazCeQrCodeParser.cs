using DespensaInteligente.Infrastructure.InvoiceScanner.Models;

namespace DespensaInteligente.Infrastructure.InvoiceScanner.Parsers;

/// <summary>
/// Parser responsável por extrair os parâmetros do QRCode da NFC-e da SEFAZ-CE.
/// </summary>
public interface ISefazCeQrCodeParser
{
    /// <summary>
    /// Interpreta a URL do QRCode e constrói o payload necessário para chamada da API interna da SEFAZ-CE.
    /// </summary>
    /// <param name="qrCodeUrl">URL lida do QRCode.</param>
    /// <returns>Payload estruturado SefazCeQrCodePayload.</returns>
    SefazCeQrCodePayload Parse(string qrCodeUrl);
}
