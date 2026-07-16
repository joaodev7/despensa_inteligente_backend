using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DespensaInteligente.Domain.Entities;

namespace DespensaInteligente.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<Categoria> Categorias { get; }
        DbSet<Item> Itens { get; }
        DbSet<NotaFiscal> NotasFiscais { get; }
        DbSet<Compra> Compras { get; }
        DbSet<CompraItem> CompraItens { get; }
        DbSet<Lote> Lotes { get; }
        DbSet<ListaManual> ListaManuais { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
