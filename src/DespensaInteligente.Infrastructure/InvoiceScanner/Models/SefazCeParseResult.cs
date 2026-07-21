namespace DespensaInteligente.Infrastructure.InvoiceScanner.Models;

/// <summary>
/// Modelo de dados intermediário para armazenar informações extraídas do DOM do HTML da SEFAZ CE.
/// </summary>
public class SefazCeParseResult
{
    public string StoreName { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public string AccessKey { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    
    public string? ConsumerCpf { get; set; }
    public string? ConsumerName { get; set; }
    public string? ConsumerAddress { get; set; }

    public List<SefazCeItemModel> Items { get; set; } = new();
}

public class SefazCeItemModel
{
    public string Description { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}
