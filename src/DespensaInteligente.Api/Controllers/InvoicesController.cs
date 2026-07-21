using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DespensaInteligente.Application.InvoiceScanner.Common;
using DespensaInteligente.Application.InvoiceScanner.DTOs;
using DespensaInteligente.Application.InvoiceScanner.Interfaces;

namespace DespensaInteligente.Api.Controllers;

[AllowAnonymous] // Permite testes locais e integração inicial com o frontend
[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceScanService _invoiceScanService;

    public InvoicesController(IInvoiceScanService invoiceScanService)
    {
        _invoiceScanService = invoiceScanService ?? throw new ArgumentNullException(nameof(invoiceScanService));
    }

    /// <summary>
    /// Importa os produtos de uma NFC-e realizando o scan do QRCode impresso na nota fiscal.
    /// </summary>
    /// <param name="request">Payload contendo a URL do QRCode da nota fiscal.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição HTTP.</param>
    /// <returns>Dados normalizados da nota fiscal e lista de produtos para conferência do usuário.</returns>
    [HttpPost("scan")]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Scan([FromBody] ScanInvoiceRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _invoiceScanService.ScanAsync(request.QrCode, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return MapErrorToResponse(result.Error);
    }

    private IActionResult MapErrorToResponse(Error error)
    {
        var errorBody = new
        {
            code = error.Code,
            message = error.Message
        };

        return error.Type switch
        {
            ErrorType.InvalidQrCode => BadRequest(errorBody),
            ErrorType.Validation => BadRequest(errorBody),
            ErrorType.UnsupportedState => BadRequest(errorBody),
            ErrorType.NotFound => NotFound(errorBody),
            ErrorType.HtmlParsingError => UnprocessableEntity(errorBody),
            ErrorType.CommunicationError => StatusCode(StatusCodes.Status502BadGateway, errorBody),
            ErrorType.Timeout => StatusCode(StatusCodes.Status504GatewayTimeout, errorBody),
            _ => StatusCode(StatusCodes.Status500InternalServerError, errorBody)
        };
    }
}
