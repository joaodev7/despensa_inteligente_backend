using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Domain.Entities;

namespace DespensaInteligente.Application.Services
{
    public interface ICompraService
    {
        Task<IEnumerable<Compra>> GetComprasAsync(string? mes, string? mercado);
        Task<Compra?> GetByIdAsync(Guid id);
        Task<Compra> CreateManualAsync(CompraInputDto input);
        Task<Compra> UpdateAsync(Guid id, CompraInputDto input);
        Task<bool> DeleteAsync(Guid id);
        Task<Guid> GetOrCreateItemFromCatalogAsync(Guid userId, string nome, string unidade);
    }

    public class CompraService : ICompraService
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public CompraService(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<IEnumerable<Compra>> GetComprasAsync(string? mes, string? mercado)
        {
            var query = _context.Compras.Include(c => c.Itens).AsQueryable();

            // Filter by month (mes: YYYY-MM)
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
            else if (string.IsNullOrWhiteSpace(mes))
            {
                // Default to current month
                filterByMonth = true;
            }

            if (filterByMonth)
            {
                query = query.Where(c => c.DataCompra.Year == year && c.DataCompra.Month == month);
            }

            // Filter by mercado (case-insensitive substring)
            if (!string.IsNullOrWhiteSpace(mercado))
            {
                var cleanMercado = mercado.Trim().ToLower();
                query = query.Where(c => c.Mercado.ToLower().Contains(cleanMercado));
            }

            return await query.OrderByDescending(c => c.DataCompra).ThenByDescending(c => c.CreatedAt).ToListAsync();
        }

        public async Task<Compra?> GetByIdAsync(Guid id)
        {
            return await _context.Compras
                .Include(c => c.Itens)
                .ThenInclude(ci => ci.Item)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Compra> CreateManualAsync(CompraInputDto input)
        {
            var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

            using var transaction = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled);

            var compra = new Compra
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Mercado = input.Mercado,
                DataCompra = input.DataCompra,
                Observacoes = input.Observacoes,
                CreatedAt = DateTimeOffset.UtcNow,
                ValorTotal = 0m
            };

            _context.Compras.Add(compra);

            decimal total = 0m;

            if (input.Itens != null)
            {
                foreach (var itemInput in input.Itens)
                {
                    // 1. Get or create item in the catalog
                    var itemId = await GetOrCreateItemFromCatalogAsync(userId, itemInput.Nome, itemInput.Unidade);

                    // Calculate totals
                    decimal precoUnitario = itemInput.PrecoUnitario ?? 0m;
                    decimal precoTotal = itemInput.PrecoTotal ?? (itemInput.Quantidade * precoUnitario);
                    total += precoTotal;

                    // 2. Create CompraItem
                    var compraItem = new CompraItem
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        CompraId = compra.Id,
                        ItemId = itemId,
                        NomeOriginal = itemInput.Nome,
                        Quantidade = itemInput.Quantidade,
                        Unidade = itemInput.Unidade,
                        PrecoUnitario = precoUnitario,
                        PrecoTotal = precoTotal
                    };
                    _context.CompraItens.Add(compraItem);

                    // 3. Create Lote in physical stock (always default local='despensa')
                    var lote = new Lote
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        ItemId = itemId,
                        CompraId = compra.Id,
                        Quantidade = itemInput.Quantidade,
                        Validade = itemInput.Validade,
                        Local = "despensa",
                        Consumido = false,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _context.Lotes.Add(lote);
                }
            }

            compra.ValorTotal = total;
            await _context.SaveChangesAsync();

            transaction.Complete();

            return compra;
        }

        public async Task<Compra> UpdateAsync(Guid id, CompraInputDto input)
        {
            var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

            var compra = await _context.Compras
                .Include(c => c.Itens)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (compra == null)
            {
                throw new KeyNotFoundException("Compra não encontrada.");
            }

            using var transaction = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled);

            // Update basic info
            compra.Mercado = input.Mercado;
            compra.DataCompra = input.DataCompra;
            compra.Observacoes = input.Observacoes;

            // Remove old CompraItems and their associated Lotes
            var oldItens = await _context.CompraItens.Where(ci => ci.CompraId == id).ToListAsync();
            _context.CompraItens.RemoveRange(oldItens);

            var oldLotes = await _context.Lotes.Where(l => l.CompraId == id).ToListAsync();
            _context.Lotes.RemoveRange(oldLotes);

            // Insert new CompraItems and Lotes
            decimal total = 0m;
            if (input.Itens != null)
            {
                foreach (var itemInput in input.Itens)
                {
                    var itemId = await GetOrCreateItemFromCatalogAsync(userId, itemInput.Nome, itemInput.Unidade);

                    decimal precoUnitario = itemInput.PrecoUnitario ?? 0m;
                    decimal precoTotal = itemInput.PrecoTotal ?? (itemInput.Quantidade * precoUnitario);
                    total += precoTotal;

                    var compraItem = new CompraItem
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        CompraId = compra.Id,
                        ItemId = itemId,
                        NomeOriginal = itemInput.Nome,
                        Quantidade = itemInput.Quantidade,
                        Unidade = itemInput.Unidade,
                        PrecoUnitario = precoUnitario,
                        PrecoTotal = precoTotal
                    };
                    _context.CompraItens.Add(compraItem);

                    var lote = new Lote
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        ItemId = itemId,
                        CompraId = compra.Id,
                        Quantidade = itemInput.Quantidade,
                        Validade = itemInput.Validade,
                        Local = "despensa",
                        Consumido = false,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _context.Lotes.Add(lote);
                }
            }

            compra.ValorTotal = total;
            await _context.SaveChangesAsync();

            transaction.Complete();

            return compra;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var compra = await _context.Compras.FirstOrDefaultAsync(c => c.Id == id);
            if (compra == null)
            {
                return false;
            }

            // Note: EF Core cascade deletes CompraItens because of configuration.
            // Lotes have a nullable CompraId. To make sure they are cleaned up, we delete them:
            var lotes = await _context.Lotes.Where(l => l.CompraId == id).ToListAsync();
            _context.Lotes.RemoveRange(lotes);

            _context.Compras.Remove(compra);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Guid> GetOrCreateItemFromCatalogAsync(Guid userId, string nome, string unidade)
        {
            var cleanNome = nome.Trim();
            var searchNome = cleanNome.ToLower();

            // Search catalog by lower(Nome) for this specific user
            var item = await _context.Itens
                .IgnoreQueryFilters() // Bypass the current request scope filter for manual verification, but enforce user match
                .FirstOrDefaultAsync(i => i.UserId == userId && i.Nome.ToLower() == searchNome);

            if (item != null)
            {
                return item.Id;
            }

            // Create new product in catalog
            var newItem = new Item
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Nome = cleanNome,
                Unidade = unidade,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                EstoqueMinimo = 0m
            };

            _context.Itens.Add(newItem);
            await _context.SaveChangesAsync();

            return newItem.Id;
        }
    }

    public class CompraInputDto
    {
        public string Mercado { get; set; } = string.Empty;
        public DateOnly DataCompra { get; set; }
        public string? Observacoes { get; set; }
        public List<CompraItemInputDto>? Itens { get; set; }
    }

    public class CompraItemInputDto
    {
        public string Nome { get; set; } = string.Empty;
        public decimal Quantidade { get; set; } = 1;
        public string Unidade { get; set; } = "un";
        public decimal? PrecoUnitario { get; set; }
        public decimal? PrecoTotal { get; set; }
        public DateOnly? Validade { get; set; }
    }
}
