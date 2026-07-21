using DespensaInteligente.Application.InvoiceScanner.DTOs;

namespace DespensaInteligente.Application.InvoiceScanner.Interfaces;

/// <summary>
/// Abstração para provedores de leitura de notas fiscais por QRCode (SEFAZ).
/// Permite extensibilidade para novos estados (CE, SP, MG, etc.) ou novas fontes (XML, SAT, PDF).
/// </summary>
public interface IInvoiceProvider
{
    /// <summary>
    /// Valida se a URL do QRCode pertence ao estado/portal atendido por esta implementação.
    /// </summary>
    /// <param name="qrCode">URL da NFC-e.</param>
    /// <returns>True se o provider for capaz de processar; caso contrário, False.</returns>
    bool CanHandle(string qrCode);

    /// <summary>
    /// Efetua o download e o parsing da NFC-e a partir da URL do QRCode.
    /// </summary>
    /// <param name="qrCode">URL da NFC-e.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    /// <returns>InvoiceDto com os dados normalizados da nota e produtos.</returns>
    Task<InvoiceDto> ReadAsync(string qrCode, CancellationToken cancellationToken);
}
