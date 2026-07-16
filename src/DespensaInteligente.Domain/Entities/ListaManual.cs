using System;

namespace DespensaInteligente.Domain.Entities
{
    public class ListaManual
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Unidade { get; set; } = "un";
        public decimal QuantidadePlanejada { get; set; } = 1;
        public decimal? QuantidadeReal { get; set; }
        public bool Pego { get; set; } = false;
        public decimal? PrecoUnitario { get; set; }
        public decimal? Preco { get; set; } // Will represent PrecoTotal if set, else calculated as QuantidadeReal * PrecoUnitario
        public string? Observacoes { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
