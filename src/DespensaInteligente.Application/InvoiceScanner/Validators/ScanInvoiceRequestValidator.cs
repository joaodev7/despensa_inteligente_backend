using FluentValidation;
using DespensaInteligente.Application.InvoiceScanner.DTOs;

namespace DespensaInteligente.Application.InvoiceScanner.Validators;

public class ScanInvoiceRequestValidator : AbstractValidator<ScanInvoiceRequestDto>
{
    public ScanInvoiceRequestValidator()
    {
        RuleFor(x => x.QrCode)
            .NotEmpty()
            .WithMessage("A URL do QRCode é obrigatória.")
            .Must(BeAValidUrl)
            .WithMessage("A URL do QRCode fornecida é inválida.");
    }

    private static bool BeAValidUrl(string qrCode)
    {
        if (string.IsNullOrWhiteSpace(qrCode))
            return false;

        return Uri.TryCreate(qrCode, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
