using Microsoft.Extensions.Logging;
using DespensaInteligente.Application.InvoiceScanner.DTOs;
using DespensaInteligente.Application.InvoiceScanner.Interfaces;
using DespensaInteligente.Infrastructure.InvoiceScanner.Http;
using DespensaInteligente.Infrastructure.InvoiceScanner.Parsers;

namespace DespensaInteligente.Infrastructure.InvoiceScanner.Providers;

/// <summary>
/// Provider de integração específico para o portal da SEFAZ Ceará (SEFAZ-CE).
/// Responsabilidades:
/// - Validar se a URL pertence ao Ceará
/// - Efetuar o download do HTML da NFC-e via IInvoiceHttpClient
/// - Invocar o parser exclusivo de Ceará (ISefazCeHtmlParser)
/// - Retornar o InvoiceDto normalizado
/// </summary>
public class SefazCeProvider : IInvoiceProvider
{
    private readonly IInvoiceHttpClient _httpClient;
    private readonly ISefazCeHtmlParser _parser;
    private readonly ILogger<SefazCeProvider> _logger;

    public SefazCeProvider(
        IInvoiceHttpClient httpClient,
        ISefazCeHtmlParser parser,
        ILogger<SefazCeProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanHandle(string qrCode)
    {
        if (string.IsNullOrWhiteSpace(qrCode)) return false;

        if (!Uri.TryCreate(qrCode, UriKind.Absolute, out var uri)) return false;

        // Domínios da SEFAZ-CE conhecidos
        var host = uri.Host.ToLowerInvariant();
        return host.EndsWith("sefaz.ce.gov.br", StringComparison.OrdinalIgnoreCase)
               || host.Contains("sefaz-ce")
               || (host.Contains("ce.gov.br") && qrCode.Contains("nfce", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<InvoiceDto> ReadAsync(string qrCode, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SefazCeProvider acionado para processar QRCode da SEFAZ CE: {QrCodeUrl}", qrCode);

        // 1. Download do HTML
        var html = await _httpClient.DownloadHtmlAsync(qrCode, cancellationToken);

        // 2. Parsing do HTML com HtmlAgilityPack
        var invoiceDomain = _parser.Parse(html);

        // 3. Mapeamento para DTO normalizado
        return new InvoiceDto(
            StoreName: invoiceDomain.StoreName,
            Cnpj: invoiceDomain.Cnpj,
            IssueDate: invoiceDomain.IssueDate,
            AccessKey: invoiceDomain.AccessKey,
            Total: invoiceDomain.Total,
            PaymentMethod: invoiceDomain.PaymentMethod,
            Consumer: new ConsumerDto(
                Cpf: invoiceDomain.Consumer.Cpf,
                Name: invoiceDomain.Consumer.Name,
                Address: invoiceDomain.Consumer.Address
            ),
            Items: invoiceDomain.Items.Select(item => new InvoiceItemDto(
                Description: item.Description,
                Code: item.Code,
                Quantity: item.Quantity,
                Unit: item.Unit,
                UnitPrice: item.UnitPrice,
                Total: item.Total
            )).ToList()
        );
    }
}
