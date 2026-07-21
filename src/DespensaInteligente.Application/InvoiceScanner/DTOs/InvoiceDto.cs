namespace DespensaInteligente.Application.InvoiceScanner.DTOs;

public record InvoiceDto(
    string StoreName,
    string Cnpj,
    DateTime IssueDate,
    string AccessKey,
    decimal Total,
    string PaymentMethod,
    ConsumerDto Consumer,
    List<InvoiceItemDto> Items
);
