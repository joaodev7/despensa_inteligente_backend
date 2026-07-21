namespace DespensaInteligente.Infrastructure.InvoiceScanner.Models;

/// <summary>
/// Payload contendo os parâmetros extraídos do QRCode da NFC-e da SEFAZ-CE.
/// </summary>
public sealed class SefazCeQrCodePayload
{
    public string ChaveAcesso { get; init; } = string.Empty;
    public int VersaoQrCode { get; init; } = 2;
    public int TipoAmbiente { get; init; } = 1;
    public string? DiaEmissao { get; init; }
    public decimal? ValorTotal { get; init; }
    public string? DigVal { get; init; }
    public string IdentificadorCSC { get; init; } = string.Empty;
    public string CodigoHash { get; init; } = string.Empty;
}
