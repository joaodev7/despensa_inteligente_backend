using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Domain.Entities;

namespace DespensaInteligente.Application.Services
{
    public interface IListaManualService
    {
        Task<IEnumerable<ListaManual>> GetAllAsync();
        Task<ListaManual?> GetByIdAsync(Guid id);
        Task<ListaManual> AddItemAsync(ListaManualInputDto input);
        Task<ListaManual> UpdateItemAsync(Guid id, ListaManualUpdateDto input);
        Task<bool> RemoveItemAsync(Guid id);
        Task<Guid> FinalizarListaAsync(FinalizarListaDto input);
    }

    public class ListaManualService : IListaManualService
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICompraService _compraService;

        public ListaManualService(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            ICompraService compraService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _compraService = compraService;
        }

        public async Task<IEnumerable<ListaManual>> GetAllAsync()
        {
            return await _context.ListaManuais
                .OrderBy(l => l.CreatedAt)
                .ToListAsync();
        }

        public async Task<ListaManual?> GetByIdAsync(Guid id)
        {
            return await _context.ListaManuais.FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<ListaManual> AddItemAsync(ListaManualInputDto input)
        {
            var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

            var item = new ListaManual
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Nome = input.Nome,
                Unidade = input.Unidade,
                QuantidadePlanejada = input.QuantidadePlanejada,
                Observacoes = input.Observacoes,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _context.ListaManuais.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public async Task<ListaManual> UpdateItemAsync(Guid id, ListaManualUpdateDto input)
        {
            var item = await _context.ListaManuais.FirstOrDefaultAsync(l => l.Id == id);
            if (item == null)
            {
                throw new KeyNotFoundException("Item não encontrado na lista.");
            }

            if (input.Nome != null) item.Nome = input.Nome;
            if (input.Unidade != null) item.Unidade = input.Unidade;
            if (input.QuantidadePlanejada.HasValue) item.QuantidadePlanejada = input.QuantidadePlanejada.Value;
            if (input.QuantidadeReal.HasValue) item.QuantidadeReal = input.QuantidadeReal.Value;
            if (input.Pego.HasValue) item.Pego = input.Pego.Value;
            if (input.PrecoUnitario.HasValue) item.PrecoUnitario = input.PrecoUnitario.Value;
            
            // Calculate Preco automatically if not set, or override
            if (input.Preco.HasValue)
            {
                item.Preco = input.Preco.Value;
            }
            else if (item.PrecoUnitario.HasValue)
            {
                var qtd = item.QuantidadeReal ?? item.QuantidadePlanejada;
                item.Preco = qtd * item.PrecoUnitario.Value;
            }

            if (input.Observacoes != null) item.Observacoes = input.Observacoes;
            item.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();
            return item;
        }

        public async Task<bool> RemoveItemAsync(Guid id)
        {
            var item = await _context.ListaManuais.FirstOrDefaultAsync(l => l.Id == id);
            if (item == null)
            {
                return false;
            }

            _context.ListaManuais.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Guid> FinalizarListaAsync(FinalizarListaDto input)
        {
            var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

            if (input.ItemIds == null || input.ItemIds.Count == 0)
            {
                throw new ArgumentException("Nenhum item selecionado para finalizar.");
            }

            using var transaction = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled);

            // Fetch the items in the list that have Pego == true and are in the selected list
            var itemsToFinalize = await _context.ListaManuais
                .Where(l => l.Pego && input.ItemIds.Contains(l.Id))
                .ToListAsync();

            if (itemsToFinalize.Count == 0)
            {
                throw new InvalidOperationException("Nenhum item marcado como 'peguei' foi encontrado na lista para finalizar.");
            }

            // Create Compra record
            var compra = new Compra
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Mercado = input.Mercado,
                DataCompra = DateOnly.FromDateTime(DateTime.Today),
                CreatedAt = DateTimeOffset.UtcNow,
                ValorTotal = 0m
            };
            _context.Compras.Add(compra);

            decimal totalGeral = 0m;

            foreach (var shoppingItem in itemsToFinalize)
            {
                // Calculate item price
                decimal precoUnitario = shoppingItem.PrecoUnitario ?? 0m;
                decimal quantidade = shoppingItem.QuantidadeReal ?? shoppingItem.QuantidadePlanejada;
                decimal precoTotal = shoppingItem.Preco ?? (quantidade * precoUnitario);
                totalGeral += precoTotal;

                // Get or create Item in catalog
                var catalogItemId = await _compraService.GetOrCreateItemFromCatalogAsync(userId, shoppingItem.Nome, shoppingItem.Unidade);

                // Create CompraItem
                var compraItem = new CompraItem
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CompraId = compra.Id,
                    ItemId = catalogItemId,
                    NomeOriginal = shoppingItem.Nome,
                    Quantidade = quantidade,
                    Unidade = shoppingItem.Unidade,
                    PrecoUnitario = precoUnitario,
                    PrecoTotal = precoTotal
                };
                _context.CompraItens.Add(compraItem);

                // Create Lote in physical pantry
                var lote = new Lote
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ItemId = catalogItemId,
                    CompraId = compra.Id,
                    Quantidade = quantidade,
                    Local = "despensa", // default local
                    Consumido = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _context.Lotes.Add(lote);
            }

            compra.ValorTotal = totalGeral;

            // Remove items from shopping list
            _context.ListaManuais.RemoveRange(itemsToFinalize);

            await _context.SaveChangesAsync();

            transaction.Complete();

            return compra.Id;
        }
    }

    public class ListaManualInputDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Unidade { get; set; } = "un";
        public decimal QuantidadePlanejada { get; set; } = 1;
        public string? Observacoes { get; set; }
    }

    public class ListaManualUpdateDto
    {
        public string? Nome { get; set; }
        public string? Unidade { get; set; }
        public decimal? QuantidadePlanejada { get; set; }
        public decimal? QuantidadeReal { get; set; }
        public bool? Pego { get; set; }
        public decimal? PrecoUnitario { get; set; }
        public decimal? Preco { get; set; }
        public string? Observacoes { get; set; }
    }

    public class FinalizarListaDto
    {
        public string Mercado { get; set; } = "Mercado";
        public List<Guid> ItemIds { get; set; } = new();
    }
}
