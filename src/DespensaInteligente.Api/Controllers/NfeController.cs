using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DespensaInteligente.Application.Common.DTOs;
using DespensaInteligente.Application.Exceptions;
using DespensaInteligente.Application.Services;
using DespensaInteligente.Domain.Entities;

namespace DespensaInteligente.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NfeController : ControllerBase
    {
        private readonly INfeService _nfeService;

        public NfeController(INfeService nfeService)
        {
            _nfeService = nfeService;
        }

        [HttpPost("consultar")]
        [ProducesResponseType(typeof(InvoiceExtractionResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Consultar([FromBody] NfeConsultaRequestDto request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _nfeService.ConsultarEExtrairAsync(request.Url, request.Chave, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return HandleLlmException(ex);
            }
        }

        [HttpPost("upload")]
        [ProducesResponseType(typeof(NfeUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Nenhum arquivo enviado." });
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _nfeService.UploadEExtrairAsync(file.FileName, stream, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return HandleLlmException(ex);
            }
        }

        [HttpPost("importar")]
        [ProducesResponseType(typeof(Compra), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Importar([FromBody] NfeImportDto request)
        {
            try
            {
                var result = await _nfeService.ImportarExtracaoAsync(request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<NotaFiscal>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetHistorico()
        {
            var result = await _nfeService.GetHistoricoAsync();
            return Ok(result);
        }

        private IActionResult HandleLlmException(Exception ex)
        {
            if (ex is InvalidApiKeyException)
            {
                return StatusCode(StatusCodes.Status401Unauthorized, new { message = ex.Message });
            }
            if (ex is ModelNotFoundException)
            {
                return StatusCode(StatusCodes.Status404NotFound, new { message = ex.Message });
            }
            if (ex is QuotaExceededException || ex is RateLimitExceededException)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
            }
            if (ex is ModelUnavailableException || ex is HighDemandException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
            }
            if (ex is LlmTimeoutException)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = ex.Message });
            }
            if (ex is LlmException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }

            return BadRequest(new { message = ex.Message });
        }
    }

    public class NfeConsultaRequestDto
    {
        public string? Url { get; set; }
        public string? Chave { get; set; }
    }
}
