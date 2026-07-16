using System;
using System.Collections.Generic;

namespace DespensaInteligente.Domain.Entities
{
    public class Compra
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Mercado { get; set; } = string.Empty;
        public DateOnly DataCompra { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public decimal ValorTotal { get; set; } = 0;
        public Guid? NotaFiscalId { get; set; }
        public string? Observacoes { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        public NotaFiscal? NotaFiscal { get; set; }
        public ICollection<CompraItem> Itens { get; set; } = new List<CompraItem>();
        public ICollection<Lote> Lotes { get; set; } = new List<Lote>();
    }
}
