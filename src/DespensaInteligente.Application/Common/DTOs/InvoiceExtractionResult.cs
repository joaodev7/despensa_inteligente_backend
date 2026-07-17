using System;
using System.Collections.Generic;

namespace DespensaInteligente.Application.Common.DTOs
{
    public class InvoiceExtractionResult
    {
        public string Estabelecimento { get; set; } = string.Empty;
        public string CNPJ { get; set; } = string.Empty;
        public DateOnly DataCompra { get; set; }
        public decimal ValorTotal { get; set; }
        public string ChaveAcesso { get; set; } = string.Empty;
        public List<InvoiceItemResult> Itens { get; set; } = new();
    }

    public class InvoiceItemResult
    {
        public string Descricao { get; set; } = string.Empty;
        public decimal Quantidade { get; set; } = 1;
        public string Unidade { get; set; } = "un";
        public decimal ValorUnitario { get; set; }
        public decimal ValorTotal { get; set; }
    }
}
