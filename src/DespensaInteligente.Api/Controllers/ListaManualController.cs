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
    [Route("api/lista-manual")]
    public class ListaManualController : ControllerBase
    {
        private readonly IListaManualService _listaService;

        public ListaManualController(IListaManualService listaService)
        {
            _listaService = listaService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ListaManual>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get()
        {
            var result = await _listaService.GetAllAsync();
            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ListaManual), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] ListaManualInputDto input)
        {
            try
            {
                var result = await _listaService.AddItemAsync(input);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ListaManual), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _listaService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound(new { message = "Item não encontrado na lista." });
            }
            return Ok(result);
        }

        [HttpPatch("{id}")]
        [ProducesResponseType(typeof(ListaManual), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid id, [FromBody] ListaManualUpdateDto input)
        {
            try
            {
                var result = await _listaService.UpdateItemAsync(id, input);
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
            var deleted = await _listaService.RemoveItemAsync(id);
            if (!deleted)
            {
                return NotFound(new { message = "Item não encontrado na lista." });
            }
            return NoContent();
        }

        [HttpPost("finalizar")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> Finalizar([FromBody] FinalizarListaDto input)
        {
            try
            {
                var compraId = await _listaService.FinalizarListaAsync(input);
                return Ok(new { compraId });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }
    }
}
