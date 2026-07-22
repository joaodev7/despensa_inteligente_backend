using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Domain.Entities;

namespace DespensaInteligente.Application.Services
{
    public interface ILoteService
    {
        Task<IEnumerable<Lote>> GetLotesAsync(string? mes, string? mercado, string? search, string? local, bool somenteAtivos);
        Task<Lote?> GetByIdAsync(Guid id);
        Task<Lote> CreateManualAsync(LoteInputDto input);
        Task<Lote> UpdateAsync(Guid id, LoteUpdateDto input);
        Task<bool> ConsumirAsync(Guid id);
        Task<bool> DeleteAsync(Guid id);
    }

    public class LoteService : ILoteService
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICompraService _compraService;

        public LoteService(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            ICompraService compraService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _compraService = compraService;
        }

        public async Task<IEnumerable<Lote>> GetLotesAsync(string? mes, string? mercado, string? search, string? local, bool somenteAtivos)
        {
            var query = _context.Lotes
                .Include(l => l.Item)
                .Include(l => l.Compra)
                .AsQueryable();

            if (somenteAtivos)
            {
                query = query.Where(l => !l.Consumido);
            }

            // Local filter
            if (!string.IsNullOrWhiteSpace(local))
            {
                query = query.Where(l => l.Local.ToLower() == local.Trim().ToLower());
            }

            // Month filter (relative to Compra.DataCompra, or Lote.CreatedAt if Compra is null)
            int year = DateTime.Today.Year;
            int month = DateTime.Today.Month;
            bool filterByMonth = false;

            if (!string.IsNullOrWhiteSpace(mes) && mes.Length == 7 && mes[4] == '-')
            {
                if (int.TryParse(mes.Substring(0, 4), out var y) && int.TryParse(mes.Substring(5, 2), out var m))
                {
                    year = y;
                    month = m;
                    filterByMonth = true;
                }
            }

            if (filterByMonth)
            {
                var startOfMonthDate = new DateOnly(year, month, 1);
                var endOfMonthDate = startOfMonthDate.AddMonths(1);
                var startOfMonthUtc = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
                var endOfMonthUtc = startOfMonthUtc.AddMonths(1);

                query = query.Where(l => 
                    (l.Compra != null && l.Compra.DataCompra >= startOfMonthDate && l.Compra.DataCompra < endOfMonthDate) ||
                    (l.Compra == null && l.CreatedAt >= startOfMonthUtc && l.CreatedAt < endOfMonthUtc)
                );
            }

            // Market filter (through Compra)
            if (!string.IsNullOrWhiteSpace(mercado))
            {
                var cleanMercado = mercado.Trim().ToLower();
                query = query.Where(l => l.Compra != null && l.Compra.Mercado.ToLower().Contains(cleanMercado));
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var cleanSearch = search.Trim().ToLower();
                query = query.Where(l => 
                    l.Item!.Nome.ToLower().Contains(cleanSearch) || 
                    (l.Item.Apelido != null && l.Item.Apelido.ToLower().Contains(cleanSearch)) || 
                    (l.Item.Marca != null && l.Item.Marca.ToLower().Contains(cleanSearch)) || 
                    (l.Apelido != null && l.Apelido.ToLower().Contains(cleanSearch)) || 
                    (l.Marca != null && l.Marca.ToLower().Contains(cleanSearch))
                );
            }

            return await query.OrderBy(l => l.Validade).ThenByDescending(l => l.CreatedAt).ToListAsync();
        }

        public async Task<Lote?> GetByIdAsync(Guid id)
        {
            return await _context.Lotes
                .Include(l => l.Item)
                .Include(l => l.Compra)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<Lote> CreateManualAsync(LoteInputDto input)
        {
            var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

            Guid itemId;
            if (input.ItemId.HasValue)
            {
                itemId = input.ItemId.Value;
            }
            else if (!string.IsNullOrWhiteSpace(input.NomeItem))
            {
                itemId = await _compraService.GetOrCreateItemFromCatalogAsync(userId, input.NomeItem, input.UnidadeItem ?? "un");
            }
            else
            {
                throw new ArgumentException("itemId ou nomeItem deve ser fornecido.");
            }

            var lote = new Lote
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ItemId = itemId,
                Quantidade = input.Quantidade,
                Validade = input.Validade,
                Apelido = input.Apelido,
                Marca = input.Marca,
                Local = input.Local ?? "despensa",
                Consumido = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _context.Lotes.Add(lote);
            await _context.SaveChangesAsync();

            return lote;
        }

        public async Task<Lote> UpdateAsync(Guid id, LoteUpdateDto input)
        {
            var lote = await _context.Lotes
                .Include(l => l.Item)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lote == null)
            {
                throw new KeyNotFoundException("Lote não encontrado.");
            }

            lote.Validade = input.Validade;
            lote.Quantidade = input.Quantidade;
            lote.Local = input.Local ?? "despensa";
            lote.Apelido = input.Apelido;
            lote.Marca = input.Marca;
            lote.UpdatedAt = DateTimeOffset.UtcNow;

            // Propagate nickname and brand to Catalog Item if checked
            if (input.PropagarAoCatalogo && lote.Item != null)
            {
                lote.Item.Apelido = input.Apelido;
                lote.Item.Marca = input.Marca;
                lote.Item.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _context.SaveChangesAsync();
            return lote;
        }

        public async Task<bool> ConsumirAsync(Guid id)
        {
            var lote = await _context.Lotes.FirstOrDefaultAsync(l => l.Id == id);
            if (lote == null)
            {
                return false;
            }

            lote.Consumido = true;
            lote.ConsumidoEm = DateOnly.FromDateTime(DateTime.Today);
            lote.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var lote = await _context.Lotes.FirstOrDefaultAsync(l => l.Id == id);
            if (lote == null)
            {
                return false;
            }

            _context.Lotes.Remove(lote);
            await _context.SaveChangesAsync();
            return true;
        }
    }

    public class LoteInputDto
    {
        public Guid? ItemId { get; set; }
        public string? NomeItem { get; set; }
        public string? UnidadeItem { get; set; }
        public decimal Quantidade { get; set; }
        public DateOnly? Validade { get; set; }
        public string? Apelido { get; set; }
        public string? Marca { get; set; }
        public string? Local { get; set; }
    }

    public class LoteUpdateDto
    {
        public decimal Quantidade { get; set; }
        public DateOnly? Validade { get; set; }
        public string? Local { get; set; }
        public string? Apelido { get; set; }
        public string? Marca { get; set; }
        public bool PropagarAoCatalogo { get; set; }
    }
}
