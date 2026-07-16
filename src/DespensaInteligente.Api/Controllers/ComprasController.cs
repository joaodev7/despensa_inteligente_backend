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
    public class ComprasController : ControllerBase
    {
        private readonly ICompraService _compraService;

        public ComprasController(ICompraService compraService)
        {
            _compraService = compraService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Compra>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get([FromQuery] string? mes, [FromQuery] string? mercado)
        {
            var result = await _compraService.GetComprasAsync(mes, mercado);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Compra), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _compraService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound(new { message = "Compra não encontrada." });
            }
            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(typeof(Compra), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CompraInputDto input)
        {
            try
            {
                var result = await _compraService.CreateManualAsync(input);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(Compra), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid id, [FromBody] CompraInputDto input)
        {
            try
            {
                var result = await _compraService.UpdateAsync(id, input);
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

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _compraService.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound(new { message = "Compra não encontrada." });
            }
            return NoContent();
        }
    }
}
