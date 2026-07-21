using DespensaInteligente.Domain.InvoiceScanner.Entities;

namespace DespensaInteligente.Infrastructure.InvoiceScanner.Parsers;

/// <summary>
/// Interface para o parser de HTML da SEFAZ Ceará.
/// </summary>
public interface ISefazCeHtmlParser
{
    /// <summary>
    /// Processa a string de HTML da NFC-e do Ceará e produz a entidade de domínio Invoice.
    /// </summary>
    /// <param name="html">Conteúdo HTML da página.</param>
    /// <returns>Objeto Invoice preenchido.</returns>
    Invoice Parse(string html);
}
