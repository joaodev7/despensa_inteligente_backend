using System;

namespace DespensaInteligente.Domain.Entities
{
    public class NotaFiscal
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string ChaveAcesso { get; set; } = string.Empty;
        public string Mercado { get; set; } = string.Empty;
        public DateOnly DataCompra { get; set; }
        public decimal ValorTotal { get; set; }
        public string? ArquivoNome { get; set; }
        public string? ArquivoPath { get; set; }
        public string? RawExtracao { get; set; } // Will be mapped to jsonb in PostgreSQL
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
