using System.Text.Json.Serialization;

namespace DespensaInteligente.Infrastructure.InvoiceScanner.Models;

/// <summary>
/// Modelo de resposta desserializado da API interna da SEFAZ-CE (/nfce/api/notasFiscal/qrcodevX/).
/// </summary>
public sealed class SefazCeApiResponse
{
    [JsonPropertyName("xml")]
    public string Xml { get; init; } = string.Empty;

    [JsonPropertyName("erro")]
    public string Erro { get; init; } = string.Empty;
}
