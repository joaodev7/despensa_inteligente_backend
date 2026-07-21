using DespensaInteligente.Application.InvoiceScanner.Common;
using DespensaInteligente.Application.InvoiceScanner.DTOs;

namespace DespensaInteligente.Application.InvoiceScanner.Interfaces;

/// <summary>
/// Serviço da camada de aplicação responsável por orquestrar o escaneamento e parsing de notas fiscais.
/// Não conhece detalhes específicos de nenhuma SEFAZ nem do formato do portal.
/// </summary>
public interface IInvoiceScanService
{
    /// <summary>
    /// Processa a URL de um QRCode de nota fiscal, localiza o provider apropriado e retorna a nota normalizada.
    /// </summary>
    /// <param name="qrCode">URL lida do QRCode.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Result encapsulando o InvoiceDto ou o erro correspondente.</returns>
    Task<Result<InvoiceDto>> ScanAsync(string qrCode, CancellationToken cancellationToken);
}
