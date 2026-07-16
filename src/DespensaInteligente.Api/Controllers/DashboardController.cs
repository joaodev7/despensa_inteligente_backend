using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DespensaInteligente.Application.Services;

namespace DespensaInteligente.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("resumo")]
        [ProducesResponseType(typeof(DashboardResumoDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetResumo([FromQuery] string? mes, [FromQuery] string? mercado)
        {
            var result = await _dashboardService.GetResumoAsync(mes, mercado);
            return Ok(result);
        }

        [HttpGet("validades-timeline")]
        [ProducesResponseType(typeof(ValidadesTimelineDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetValidadesTimeline([FromQuery] string? mes, [FromQuery] string? mercado)
        {
            var result = await _dashboardService.GetValidadesTimelineAsync(mes, mercado);
            return Ok(result);
        }

        [HttpGet("gasto-mensal-por-mercado")]
        [ProducesResponseType(typeof(IEnumerable<Dictionary<string, object>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetGastoMensalPorMercado([FromQuery] string? mes, [FromQuery] string? mercado)
        {
            var result = await _dashboardService.GetGastoMensalPorMercadoAsync(mes, mercado);
            return Ok(result);
        }

        [HttpGet("durabilidade")]
        [ProducesResponseType(typeof(DurabilidadeResultDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDurabilidade([FromQuery] string? mes, [FromQuery] string? mercado)
        {
            var result = await _dashboardService.GetDurabilidadeAsync(mes, mercado);
            return Ok(result);
        }

        [HttpGet("recompra")]
        [ProducesResponseType(typeof(IEnumerable<RecompraItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRecompra([FromQuery] string? mes, [FromQuery] string? mercado)
        {
            var result = await _dashboardService.GetRecompraSuggestionsAsync(mes, mercado);
            return Ok(result);
        }
    }
}
