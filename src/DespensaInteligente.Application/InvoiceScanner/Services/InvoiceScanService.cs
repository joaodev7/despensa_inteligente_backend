using FluentValidation;
using Microsoft.Extensions.Logging;
using DespensaInteligente.Application.InvoiceScanner.Common;
using DespensaInteligente.Application.InvoiceScanner.DTOs;
using DespensaInteligente.Application.InvoiceScanner.Interfaces;

namespace DespensaInteligente.Application.InvoiceScanner.Services;

/// <summary>
/// Serviço central de escaneamento de notas fiscais.
/// Orquestra a seleção do Provider adequado e converte falhas conhecidas em Resultados estruturados (Result Pattern).
/// </summary>
public class InvoiceScanService : IInvoiceScanService
{
    private readonly IEnumerable<IInvoiceProvider> _providers;
    private readonly IValidator<ScanInvoiceRequestDto> _validator;
    private readonly ILogger<InvoiceScanService> _logger;

    public InvoiceScanService(
        IEnumerable<IInvoiceProvider> providers,
        IValidator<ScanInvoiceRequestDto> validator,
        ILogger<InvoiceScanService> logger)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<InvoiceDto>> ScanAsync(string qrCode, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando escaneamento de nota fiscal via QRCode. URL: {QrCodeUrl}", qrCode);

        // 1. Validação de entrada
        var validationResult = await _validator.ValidateAsync(new ScanInvoiceRequestDto(qrCode), cancellationToken);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors.First().ErrorMessage;
            _logger.LogWarning("QRCode informado é inválido: {ErrorMessage}", firstError);
            return Result<InvoiceDto>.Failure(Error.InvalidQrCode("INVALID_QR_CODE", firstError));
        }

        // 2. Seleção do Provider apropriado
        var provider = _providers.FirstOrDefault(p => p.CanHandle(qrCode));
        if (provider == null)
        {
            _logger.LogWarning("Nenhum provedor (Provider) foi encontrado para atender a URL: {QrCodeUrl}", qrCode);
            return Result<InvoiceDto>.Failure(Error.UnsupportedState(
                "UNSUPPORTED_STATE",
                "O estado ou portal da SEFAZ referente ao QRCode informado não é suportado no momento."));
        }

        _logger.LogInformation("Provider selecionado: {ProviderType}", provider.GetType().Name);

        // 3. Execução da leitura com tratamento resiliente de erros
        try
        {
            var invoiceDto = await provider.ReadAsync(qrCode, cancellationToken);
            _logger.LogInformation("Nota fiscal processada com sucesso. Loja: {StoreName}, Itens: {Count}",
                invoiceDto.StoreName, invoiceDto.Items.Count);

            return Result<InvoiceDto>.Success(invoiceDto);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("A operação de leitura da nota fiscal foi cancelada pelo cliente.");
            throw;
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timeout ocorrido ao tentar consultar o portal da SEFAZ.");
            return Result<InvoiceDto>.Failure(Error.Timeout("TIMEOUT", "Tempo limite excedido ao comunicar com o servidor da SEFAZ. Tente novamente mais tarde."));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("não encontrada", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Nota fiscal não encontrada no portal da SEFAZ.");
            return Result<InvoiceDto>.Failure(Error.NotFound("INVOICE_NOT_FOUND", "A nota fiscal consultada não foi encontrada ou ainda não foi processada pela SEFAZ."));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao interpretar o HTML do portal da SEFAZ.");
            return Result<InvoiceDto>.Failure(Error.HtmlParsingError("HTML_PARSING_ERROR", $"Falha ao interpretar o HTML da SEFAZ: {ex.Message}"));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro de comunicação HTTP com a SEFAZ.");
            return Result<InvoiceDto>.Failure(Error.CommunicationError("SEFAZ_COMMUNICATION_ERROR", "Falha de comunicação ao conectar com os servidores da SEFAZ."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao realizar a leitura da nota fiscal.");
            return Result<InvoiceDto>.Failure(Error.Unexpected("UNEXPECTED_ERROR", $"Ocorreu um erro inesperado ao importar a nota fiscal: {ex.Message}"));
        }
    }
}
