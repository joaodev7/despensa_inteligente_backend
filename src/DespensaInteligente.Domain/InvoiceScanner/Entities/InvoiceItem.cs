namespace DespensaInteligente.Domain.InvoiceScanner.Entities;

/// <summary>
/// Representa um item/produto pertencente a uma nota fiscal (NFC-e).
/// </summary>
public class InvoiceItem
{
    public Guid Id { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public decimal Total { get; private set; }

    private InvoiceItem() { }

    public InvoiceItem(string description, string code, decimal quantity, string unit, decimal unitPrice, decimal total)
    {
        Id = Guid.NewGuid();
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("A descrição do item não pode ser vazia.", nameof(description)) : description;
        Code = code ?? string.Empty;
        Quantity = quantity;
        Unit = string.IsNullOrWhiteSpace(unit) ? "UN" : unit;
        UnitPrice = unitPrice;
        Total = total > 0 ? total : Math.Round(quantity * unitPrice, 2);
    }
}
