namespace DespensaInteligente.Domain.InvoiceScanner.ValueObjects;

/// <summary>
/// Representa os dados do consumidor associados à nota fiscal.
/// </summary>
public record Consumer
{
    public string? Cpf { get; init; }
    public string? Name { get; init; }
    public string? Address { get; init; }

    public Consumer() { }

    public Consumer(string? cpf, string? name, string? address)
    {
        Cpf = cpf;
        Name = name;
        Address = address;
    }
}
