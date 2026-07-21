namespace DespensaInteligente.Application.InvoiceScanner.Common;

/// <summary>
/// Estrutura imutável para representação de erros no Result Pattern.
/// </summary>
public record Error(string Code, string Message, ErrorType Type = ErrorType.Unexpected)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Unexpected);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error UnsupportedState(string code, string message) => new(code, message, ErrorType.UnsupportedState);
    public static Error CommunicationError(string code, string message) => new(code, message, ErrorType.CommunicationError);
    public static Error HtmlParsingError(string code, string message) => new(code, message, ErrorType.HtmlParsingError);
    public static Error Timeout(string code, string message) => new(code, message, ErrorType.Timeout);
    public static Error InvalidQrCode(string code, string message) => new(code, message, ErrorType.InvalidQrCode);
    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}
