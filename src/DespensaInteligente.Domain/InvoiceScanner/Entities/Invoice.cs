using DespensaInteligente.Domain.InvoiceScanner.ValueObjects;

namespace DespensaInteligente.Domain.InvoiceScanner.Entities;

/// <summary>
/// Raiz de Agregação (Aggregate Root) representando uma nota fiscal NFC-e.
/// </summary>
public class Invoice
{
    public Guid Id { get; private set; }
    public string StoreName { get; private set; } = string.Empty;
    public string Cnpj { get; private set; } = string.Empty;
    public DateTime IssueDate { get; private set; }
    public string AccessKey { get; private set; } = string.Empty;
    public decimal Total { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty;
    public Consumer Consumer { get; private set; } = new();

    private readonly List<InvoiceItem> _items = new();
    public IReadOnlyCollection<InvoiceItem> Items => _items.AsReadOnly();

    private Invoice() { }

    public Invoice(
        string storeName,
        string cnpj,
        DateTime issueDate,
        string accessKey,
        decimal total,
        string paymentMethod,
        Consumer? consumer = null,
        IEnumerable<InvoiceItem>? items = null)
    {
        Id = Guid.NewGuid();
        StoreName = storeName ?? string.Empty;
        Cnpj = cnpj ?? string.Empty;
        IssueDate = issueDate;
        AccessKey = accessKey ?? string.Empty;
        Total = total;
        PaymentMethod = paymentMethod ?? string.Empty;
        Consumer = consumer ?? new Consumer();

        if (items != null)
        {
            _items.AddRange(items);
        }
    }

    public void AddItem(InvoiceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
    }
}
