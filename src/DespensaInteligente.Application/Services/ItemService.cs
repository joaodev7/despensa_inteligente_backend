using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Domain.Entities;

namespace DespensaInteligente.Application.Services
{
    public interface IItemService
    {
        Task<IEnumerable<Item>> GetItensAsync(string? search, Guid? categoriaId);
        Task<Item?> GetByIdAsync(Guid id);
        Task<Item> CreateAsync(Item item);
        Task<Item> UpdateAsync(Guid id, Item item);
        Task<bool> DeleteAsync(Guid id);
    }

    public class ItemService : IItemService
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public ItemService(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<IEnumerable<Item>> GetItensAsync(string? search, Guid? categoriaId)
        {
            var query = _context.Itens.Include(i => i.Categoria).AsQueryable();

            if (categoriaId.HasValue)
            {
                query = query.Where(i => i.CategoriaId == categoriaId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var cleanSearch = search.Trim().ToLower();
                query = query.Where(i => 
                    i.Nome.ToLower().Contains(cleanSearch) || 
                    (i.Apelido != null && i.Apelido.ToLower().Contains(cleanSearch)) || 
                    (i.Marca != null && i.Marca.ToLower().Contains(cleanSearch)));
            }

            return await query.OrderBy(i => i.Nome).ToListAsync();
        }

        public async Task<Item?> GetByIdAsync(Guid id)
        {
            return await _context.Itens
                .Include(i => i.Categoria)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Item> CreateAsync(Item item)
        {
            item.Id = Guid.NewGuid();
            item.UserId = _currentUserService.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");
            item.CreatedAt = DateTimeOffset.UtcNow;
            item.UpdatedAt = DateTimeOffset.UtcNow;

            _context.Itens.Add(item);
            await _context.SaveChangesAsync();

            return item;
        }

        public async Task<Item> UpdateAsync(Guid id, Item item)
        {
            var entity = await _context.Itens.FirstOrDefaultAsync(i => i.Id == id);
            if (entity == null)
            {
                throw new KeyNotFoundException("Item não encontrado no catálogo.");
            }

            entity.Nome = item.Nome;
            entity.Apelido = item.Apelido;
            entity.Marca = item.Marca;
            entity.CategoriaId = item.CategoriaId;
            entity.Unidade = item.Unidade;
            entity.DuracaoDias = item.DuracaoDias;
            entity.EstoqueMinimo = item.EstoqueMinimo;
            entity.Observacoes = item.Observacoes;
            entity.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var entity = await _context.Itens.FirstOrDefaultAsync(i => i.Id == id);
            if (entity == null)
            {
                return false;
            }

            _context.Itens.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
