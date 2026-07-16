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
    public class ItensController : ControllerBase
    {
        private readonly IItemService _itemService;

        public ItensController(IItemService itemService)
        {
            _itemService = itemService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Item>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get([FromQuery] string? search, [FromQuery] Guid? categoriaId)
        {
            var result = await _itemService.GetItensAsync(search, categoriaId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Item), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _itemService.GetByIdAsync(id);
            if (result == null)
            {
                return NotFound(new { message = "Item não encontrado no catálogo." });
            }
            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(typeof(Item), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] Item item)
        {
            try
            {
                var result = await _itemService.CreateAsync(item);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(Item), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(Guid id, [FromBody] Item item)
        {
            try
            {
                var result = await _itemService.UpdateAsync(id, item);
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
            var deleted = await _itemService.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound(new { message = "Item não encontrado no catálogo." });
            }
            return NoContent();
        }
    }
}
