using System;
using System.Collections.Generic;

namespace DespensaInteligente.Domain.Entities
{
    public class Categoria
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Cor { get; set; } = "#94a3b8";
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        public ICollection<Item> Itens { get; set; } = new List<Item>();
    }
}
