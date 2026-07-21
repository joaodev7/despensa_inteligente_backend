using Microsoft.Extensions.Logging;
using DespensaInteligente.Application.InvoiceScanner.DTOs;
using DespensaInteligente.Application.InvoiceScanner.Interfaces;
using DespensaInteligente.Infrastructure.InvoiceScanner.Http;
using DespensaInteligente.Infrastructure.InvoiceScanner.Parsers;

namespace DespensaInteligente.Infrastructure.InvoiceScanner.Providers;

/// <summary>
/// Provider de integração específico para a SEFAZ Ceará (SEFAZ-CE).
/// Fluxo:
/// QRCode URL -> SefazCeQrCodeParser -> SefazCeQrCodePayload -> SefazCeApiClient (POST) -> SefazCeApiResponse.Xml -> SefazCeHtmlParser -> InvoiceDto
/// </summary>
public class SefazCeProvider : IInvoiceProvider
{
    private readonly ISefazCeQrCodeParser _qrCodeParser;
    private readonly ISefazCeApiClient _apiClient;
    private readonly ISefazCeHtmlParser _htmlParser;
    private readonly ILogger<SefazCeProvider> _logger;

    public SefazCeProvider(
        ISefazCeQrCodeParser qrCodeParser,
        ISefazCeApiClient apiClient,
        ISefazCeHtmlParser htmlParser,
        ILogger<SefazCeProvider> logger)
    {
        _qrCodeParser = qrCodeParser ?? throw new ArgumentNullException(nameof(qrCodeParser));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _htmlParser = htmlParser ?? throw new ArgumentNullException(nameof(htmlParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanHandle(string qrCode)
    {
        if (string.IsNullOrWhiteSpace(qrCode)) return false;

        if (!Uri.TryCreate(qrCode, UriKind.Absolute, out var uri)) return false;

        var host = uri.Host.ToLowerInvariant();
        return host.EndsWith("sefaz.ce.gov.br", StringComparison.OrdinalIgnoreCase)
               || host.Contains("sefaz-ce")
               || (host.Contains("ce.gov.br") && qrCode.Contains("nfce", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<InvoiceDto> ReadAsync(string qrCode, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SefazCeProvider acionado para processar QRCode da SEFAZ CE: {QrCodeUrl}", qrCode);

        // 1. Extração dos parâmetros do QRCode (Chave, Versão, CSC, Hash)
        var payload = _qrCodeParser.Parse(qrCode);

        // 2. Chamada POST para a API REST oficial da SEFAZ-CE (/nfce/api/notasFiscal/qrcodevX/)
        var apiResponse = await _apiClient.FetchInvoiceHtmlAsync(payload, cancellationToken);

        // 3. Parsing do HTML contido na propriedade 'xml' do JSON de resposta
        var invoiceDomain = _htmlParser.Parse(apiResponse.Xml);

        // 4. Mapeamento para o DTO normalizado da aplicação
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
