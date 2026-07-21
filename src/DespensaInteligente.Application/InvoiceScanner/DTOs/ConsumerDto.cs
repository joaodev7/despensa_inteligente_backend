namespace DespensaInteligente.Application.InvoiceScanner.DTOs;

public record ConsumerDto(
    string? Cpf,
    string? Name,
    string? Address
);
