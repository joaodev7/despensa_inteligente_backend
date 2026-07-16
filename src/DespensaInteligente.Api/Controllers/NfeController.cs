using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DespensaInteligente.Application.Common.DTOs;
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
        [ProducesResponseType(typeof(NfeExtractionResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Consultar([FromBody] NfeConsultaRequestDto request)
        {
            try
            {
                var result = await _nfeService.ConsultarEExtrairAsync(request.Url, request.Chave);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("upload")]
        [ProducesResponseType(typeof(NfeUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Nenhum arquivo enviado." });
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _nfeService.UploadEExtrairAsync(file.FileName, stream);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
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
    }

    public class NfeConsultaRequestDto
    {
        public string? Url { get; set; }
        public string? Chave { get; set; }
    }
}
