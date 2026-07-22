using System;
using System.Text.Json.Serialization;

namespace DespensaInteligente.Domain.Entities
{
    public class CompraItem
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid CompraId { get; set; }
        public Guid? ItemId { get; set; }
        public string NomeOriginal { get; set; } = string.Empty;
        public decimal Quantidade { get; set; } = 1;
        public string Unidade { get; set; } = "un";
        public decimal? PrecoUnitario { get; set; }
        public decimal? PrecoTotal { get; set; }

        // Navigation properties
        [JsonIgnore]
        public Compra? Compra { get; set; }
        [JsonIgnore]
        public Item? Item { get; set; }
    }
}
