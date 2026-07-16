using System;
using System.Collections.Generic;

namespace DespensaInteligente.Application.Common.DTOs
{
    public class NfeExtractionResultDto
    {
        public string Mercado { get; set; } = string.Empty;
        public DateOnly DataCompra { get; set; }
        public string ChaveAcesso { get; set; } = string.Empty;
        public decimal ValorTotal { get; set; }
        public List<NfeExtractionItemDto> Itens { get; set; } = new();
    }

    public class NfeExtractionItemDto
    {
        public string Nome { get; set; } = string.Empty;
        public decimal Quantidade { get; set; } = 1;
        public string Unidade { get; set; } = "un";
        public decimal PrecoUnitario { get; set; }
        public decimal PrecoTotal { get; set; }
    }
}
