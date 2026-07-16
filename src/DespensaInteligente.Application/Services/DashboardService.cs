using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Domain.Entities;

namespace DespensaInteligente.Application.Services
{
    public interface IDashboardService
    {
        Task<DashboardResumoDto> GetResumoAsync(string? mes, string? mercado);
        Task<ValidadesTimelineDto> GetValidadesTimelineAsync(string? mes, string? mercado);
        Task<IEnumerable<Dictionary<string, object>>> GetGastoMensalPorMercadoAsync(string? mes, string? mercado);
        Task<DurabilidadeResultDto> GetDurabilidadeAsync(string? mes, string? mercado);
        Task<IEnumerable<RecompraItemDto>> GetRecompraSuggestionsAsync(string? mes, string? mercado);
    }

    public class DashboardService : IDashboardService
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public DashboardService(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        private (int Year, int Month, bool Filtered) ParseMes(string? mes)
        {
            int year = DateTime.Today.Year;
            int month = DateTime.Today.Month;
            bool filtered = false;

            if (!string.IsNullOrWhiteSpace(mes) && mes.Length == 7 && mes[4] == '-')
            {
                if (int.TryParse(mes.Substring(0, 4), out var y) && int.TryParse(mes.Substring(5, 2), out var m))
                {
                    year = y;
                    month = m;
                    filtered = true;
                }
            }
            else if (string.IsNullOrWhiteSpace(mes))
            {
                filtered = true; // default is current month
            }

            return (year, month, filtered);
        }

        public async Task<DashboardResumoDto> GetResumoAsync(string? mes, string? mercado)
        {
            var (year, month, filterByMonth) = ParseMes(mes);

            // Filter purchases
            var comprasQuery = _context.Compras.AsQueryable();
            if (filterByMonth)
            {
                comprasQuery = comprasQuery.Where(c => c.DataCompra.Year == year && c.DataCompra.Month == month);
            }
            if (!string.IsNullOrWhiteSpace(mercado))
            {
                var cleanMercado = mercado.Trim().ToLower();
                comprasQuery = comprasQuery.Where(c => c.Mercado.ToLower().Contains(cleanMercado));
            }

            var totalGasto = await comprasQuery.SumAsync(c => c.ValorTotal);
            var qtdCompras = await comprasQuery.CountAsync();

            // Filter active lots in general
            var activeLotesQuery = _context.Lotes.Where(l => !l.Consumido);
            var qtdLotesAtivos = await activeLotesQuery.CountAsync();

            // Near-expiration lots (expires in 7 days or less)
            var today = DateOnly.FromDateTime(DateTime.Today);
            var sevenDaysLater = today.AddDays(7);

            var proximosVencimentos = await _context.Lotes
                .Include(l => l.Item)
                .Where(l => !l.Consumido && l.Validade != null && l.Validade <= sevenDaysLater)
                .OrderBy(l => l.Validade)
                .Take(5)
                .Select(l => new LoteResumoDto(
                    l.Id,
                    l.Item!.Nome,
                    l.Apelido ?? l.Item.Apelido,
                    l.Validade!.Value,
                    l.Quantidade,
                    l.Local
                ))
                .ToListAsync();

            return new DashboardResumoDto(totalGasto, qtdCompras, qtdLotesAtivos, proximosVencimentos);
        }

        public async Task<ValidadesTimelineDto> GetValidadesTimelineAsync(string? mes, string? mercado)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            // Fetch all active lots
            var lotes = await _context.Lotes
                .Include(l => l.Item)
                .Include(l => l.Compra)
                .Where(l => !l.Consumido && l.Validade != null)
                .ToListAsync();

            // Filter lists based on global parameters if required
            var (year, month, filterByMonth) = ParseMes(mes);
            if (filterByMonth)
            {
                lotes = lotes.Where(l => 
                    (l.Compra != null && l.Compra.DataCompra.Year == year && l.Compra.DataCompra.Month == month) ||
                    (l.Compra == null && l.CreatedAt.Year == year && l.CreatedAt.Month == month)
                ).ToList();
            }

            if (!string.IsNullOrWhiteSpace(mercado))
            {
                var cleanMercado = mercado.Trim().ToLower();
                lotes = lotes.Where(l => l.Compra != null && l.Compra.Mercado.ToLower().Contains(cleanMercado)).ToList();
            }

            int vencidos = 0;
            int zeroA7 = 0;
            int oitoA30 = 0;
            int trintaUmA60 = 0;
            int sessentaUmA90 = 0;
            int maisDe90 = 0;

            foreach (var lote in lotes)
            {
                var validade = lote.Validade!.Value;
                if (validade < today)
                {
                    vencidos++;
                }
                else
                {
                    int diffDays = validade.DayNumber - today.DayNumber;
                    if (diffDays <= 7) zeroA7++;
                    else if (diffDays <= 30) oitoA30++;
                    else if (diffDays <= 60) trintaUmA60++;
                    else if (diffDays <= 90) sessentaUmA90++;
                    else maisDe90++;
                }
            }

            return new ValidadesTimelineDto(vencidos, zeroA7, oitoA30, trintaUmA60, sessentaUmA90, maisDe90);
        }

        public async Task<IEnumerable<Dictionary<string, object>>> GetGastoMensalPorMercadoAsync(string? mes, string? mercado)
        {
            // Gather purchases for the last 6 months
            var today = DateTime.Today;
            var startDate = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);

            var purchases = await _context.Compras
                .Where(c => c.DataCompra >= startDate)
                .ToListAsync();

            // Apply manual filters
            if (!string.IsNullOrWhiteSpace(mercado))
            {
                var cleanMercado = mercado.Trim().ToLower();
                purchases = purchases.Where(c => c.Mercado.ToLower().Contains(cleanMercado)).ToList();
            }

            // Group by month and market
            var monthlyGroups = purchases
                .GroupBy(c => $"{c.DataCompra.Year}-{c.DataCompra.Month:D2}")
                .OrderBy(g => g.Key);

            var result = new List<Dictionary<string, object>>();

            foreach (var group in monthlyGroups)
            {
                var entry = new Dictionary<string, object>
                {
                    { "mes", group.Key }
                };

                var marketExpenses = group
                    .GroupBy(c => c.Mercado)
                    .ToDictionary(g => g.Key, g => g.Sum(c => c.ValorTotal));

                foreach (var me in marketExpenses)
                {
                    entry[me.Key] = me.Value;
                }

                result.Add(entry);
            }

            return result;
        }

        public async Task<DurabilidadeResultDto> GetDurabilidadeAsync(string? mes, string? mercado)
        {
            // Durability calculation for items
            // Durabilidade = average of (consumido_em - created_at) in days for consumed lots
            var lotes = await _context.Lotes
                .Include(l => l.Item)
                .Include(l => l.Compra)
                .Where(l => l.Consumido && l.ConsumidoEm != null)
                .ToListAsync();

            // Filter based on input if needed
            var (year, month, filterByMonth) = ParseMes(mes);
            if (filterByMonth)
            {
                lotes = lotes.Where(l => 
                    (l.Compra != null && l.Compra.DataCompra.Year == year && l.Compra.DataCompra.Month == month) ||
                    (l.Compra == null && l.CreatedAt.Year == year && l.CreatedAt.Month == month)
                ).ToList();
            }
            if (!string.IsNullOrWhiteSpace(mercado))
            {
                var cleanMercado = mercado.Trim().ToLower();
                lotes = lotes.Where(l => l.Compra != null && l.Compra.Mercado.ToLower().Contains(cleanMercado)).ToList();
            }

            if (lotes.Count == 0)
            {
                return new DurabilidadeResultDto(new List<ItemDurabilidadeDto>(), new List<ItemDurabilidadeDto>());
            }

            var itemGroups = lotes
                .GroupBy(l => new { l.ItemId, l.Item!.Nome, Apelido = l.Item.Apelido ?? l.Item.Nome })
                .Select(g => {
                    double avgDays = g.Average(l => {
                        var createdDate = DateOnly.FromDateTime(l.CreatedAt.Date);
                        return l.ConsumidoEm!.Value.DayNumber - createdDate.DayNumber;
                    });
                    // Avoid negative spans due to clock skew or manual dates
                    avgDays = Math.Max(0, avgDays);
                    return new ItemDurabilidadeDto(g.Key.ItemId, g.Key.Nome, g.Key.Apelido, Math.Round(avgDays, 1));
                })
                .ToList();

            var maisDuraveis = itemGroups.OrderByDescending(i => i.MediaDias).Take(5).ToList();
            var menosDuraveis = itemGroups.OrderBy(i => i.MediaDias).Take(5).ToList();

            return new DurabilidadeResultDto(maisDuraveis, menosDuraveis);
        }

        public async Task<IEnumerable<RecompraItemDto>> GetRecompraSuggestionsAsync(string? mes, string? mercado)
        {
            var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

            // 1. Get all catalog items
            var items = await _context.Itens
                .Include(i => i.Lotes)
                .Include(i => i.CompraItens)
                .ThenInclude(ci => ci.Compra)
                .ToListAsync();

            // 2. Identify items that are OUT OF STOCK (have no active lotes)
            var outOfStockItems = items.Where(i => !i.Lotes.Any(l => !l.Consumido)).ToList();

            var suggestions = new List<RecompraItemDto>();
            var today = DateOnly.FromDateTime(DateTime.Today);

            foreach (var item in outOfStockItems)
            {
                // Find all historical purchase dates for this item
                var purchases = item.CompraItens
                    .Where(ci => ci.Compra != null)
                    .Select(ci => ci.Compra!)
                    .OrderBy(c => c.DataCompra)
                    .ToList();

                if (purchases.Count == 0)
                {
                    // Never bought or bought outside tracked compras. 
                    // Add default stats
                    suggestions.Add(new RecompraItemDto(
                        item.Id,
                        item.Nome,
                        item.Apelido,
                        null, // last purchase date
                        30,   // default frequency (30 days)
                        0     // overdue days
                    ));
                    continue;
                }

                var lastPurchase = purchases.Last();
                DateOnly lastPurchaseDate = lastPurchase.DataCompra;

                // Calculate historical frequency (average days between consecutive purchases)
                double avgFrequency = 30; // default to 30 days if only 1 purchase
                if (purchases.Count > 1)
                {
                    int totalDaysSpan = purchases.Last().DataCompra.DayNumber - purchases.First().DataCompra.DayNumber;
                    int purchaseIntervals = purchases.Count - 1;
                    avgFrequency = purchaseIntervals > 0 ? (double)totalDaysSpan / purchaseIntervals : 30;
                }

                // If item has a custom DuracaoDias, let's use it as frequency override!
                if (item.DuracaoDias.HasValue && item.DuracaoDias.Value > 0)
                {
                    avgFrequency = item.DuracaoDias.Value;
                }

                // Days since last purchase
                int daysSinceLastPurchase = today.DayNumber - lastPurchaseDate.DayNumber;

                // Overdue days
                double overdueDays = daysSinceLastPurchase - avgFrequency;

                suggestions.Add(new RecompraItemDto(
                    item.Id,
                    item.Nome,
                    item.Apelido,
                    lastPurchaseDate,
                    Math.Round(avgFrequency, 1),
                    Math.Max(0, (int)Math.Round(overdueDays))
                ));
            }

            // Order by overdue days (most overdue first), then by name
            return suggestions
                .OrderByDescending(s => s.DiasAtraso)
                .ThenBy(s => s.Nome);
        }
    }

    // DTO Definitions
    public record DashboardResumoDto(decimal TotalGasto, int QtdCompras, int QtdLotesAtivos, IEnumerable<LoteResumoDto> ProximosVencimentos);
    public record LoteResumoDto(Guid LoteId, string ItemNome, string? Apelido, DateOnly Validade, decimal Quantidade, string Local);
    public record ValidadesTimelineDto(int Vencidos, int ZeroA7, int OitoA30, int TrintaUmA60, int SessentaUmA90, int MaisDe90);
    public record ItemDurabilidadeDto(Guid ItemId, string Nome, string Apelido, double MediaDias);
    public record DurabilidadeResultDto(IEnumerable<ItemDurabilidadeDto> MaisDuraveis, IEnumerable<ItemDurabilidadeDto> MenosDuraveis);
    public record RecompraItemDto(Guid ItemId, string Nome, string? Apelido, DateOnly? UltimaCompra, double FrequenciaMedia, int DiasAtraso);
}
