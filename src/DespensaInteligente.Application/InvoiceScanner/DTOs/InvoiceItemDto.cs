namespace DespensaInteligente.Application.InvoiceScanner.DTOs;

public record InvoiceItemDto(
    string Description,
    string Code,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal Total
);
