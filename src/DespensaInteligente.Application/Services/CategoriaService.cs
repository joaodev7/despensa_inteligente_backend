using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Domain.Entities;

namespace DespensaInteligente.Application.Services
{
    public interface ICategoriaService
    {
        Task<IEnumerable<Categoria>> GetAllAsync();
        Task<Categoria?> GetByIdAsync(Guid id);
        Task<Categoria> CreateAsync(Categoria categoria);
        Task<Categoria> UpdateAsync(Guid id, Categoria categoria);
        Task<bool> DeleteAsync(Guid id);
    }

    public class CategoriaService : ICategoriaService
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public CategoriaService(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<IEnumerable<Categoria>> GetAllAsync()
        {
            return await _context.Categorias.ToListAsync();
        }

        public async Task<Categoria?> GetByIdAsync(Guid id)
        {
            return await _context.Categorias.FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Categoria> CreateAsync(Categoria categoria)
        {
            categoria.Id = Guid.NewGuid();
            categoria.UserId = _currentUserService.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");
            categoria.CreatedAt = DateTimeOffset.UtcNow;

            _context.Categorias.Add(categoria);
            await _context.SaveChangesAsync();

            return categoria;
        }

        public async Task<Categoria> UpdateAsync(Guid id, Categoria categoria)
        {
            var entity = await _context.Categorias.FirstOrDefaultAsync(c => c.Id == id);
            if (entity == null)
            {
                throw new KeyNotFoundException("Categoria não encontrada.");
            }

            entity.Nome = categoria.Nome;
            entity.Cor = categoria.Cor;

            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var entity = await _context.Categorias.FirstOrDefaultAsync(c => c.Id == id);
            if (entity == null)
            {
                return false;
            }

            _context.Categorias.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
