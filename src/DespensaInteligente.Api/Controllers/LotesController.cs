using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DespensaInteligente.Application.Services;
using DespensaInteligente.Domain.Entities;

namespace DespensaInteligente.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class LotesController : ControllerBase
    {
        private readonly ILoteService _loteService;

        public LotesController(ILoteService loteService)
        {
            _loteService = loteService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Lote>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(
            [FromQuery] string? mes,
            [FromQuery] string? mercado,
            [FromQuery] string? search,
            [FromQuery] string? local,
            [FromQuery] bool somenteAtivos = true)
        {
            var result = await _loteService.GetLotesAsync(mes, mercado, search, local, somenteAtivos);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Lote), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _loteService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound(new { message = "Lote não encontrado." });
            }
            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(typeof(Lote), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] LoteInputDto input)
        {
            try
            {
                var result = await _loteService.CreateManualAsync(input);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPatch("{id}")]
        [ProducesResponseType(typeof(Lote), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid id, [FromBody] LoteUpdateDto input)
        {
            try
            {
                var result = await _loteService.UpdateAsync(id, input);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/consumir")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Consumir(Guid id)
        {
            var result = await _loteService.ConsumirAsync(id);
            if (!result)
            {
                return NotFound(new { message = "Lote não encontrado." });
            }
            return Ok(new { message = "Lote marcado como consumido." });
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _loteService.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound(new { message = "Lote não encontrado." });
            }
            return NoContent();
        }
    }
}
