using System;
using System.Text.Json.Serialization;

namespace DespensaInteligente.Domain.Entities
{
    public class Lote
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid ItemId { get; set; }
        public Guid? CompraId { get; set; }
        public decimal Quantidade { get; set; }
        public DateOnly? Validade { get; set; }
        public string? Apelido { get; set; }
        public string? Marca { get; set; }
        public string Local { get; set; } = "despensa";
        public DateOnly? AbertoEm { get; set; }
        public bool Consumido { get; set; } = false;
        public DateOnly? ConsumidoEm { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        [JsonIgnore]
        public Item? Item { get; set; }
        [JsonIgnore]
        public Compra? Compra { get; set; }
    }
}
