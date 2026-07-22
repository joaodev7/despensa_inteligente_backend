using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DespensaInteligente.Domain.Entities
{
    public class Item
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Apelido { get; set; }
        public string? Marca { get; set; }
        public Guid? CategoriaId { get; set; }
        public string Unidade { get; set; } = "un";
        public int? DuracaoDias { get; set; }
        public decimal EstoqueMinimo { get; set; } = 0;
        public string? Observacoes { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        public Categoria? Categoria { get; set; }

        [JsonIgnore]
        public ICollection<Lote> Lotes { get; set; } = new List<Lote>();

        [JsonIgnore]
        public ICollection<CompraItem> CompraItens { get; set; } = new List<CompraItem>();
    }
}
