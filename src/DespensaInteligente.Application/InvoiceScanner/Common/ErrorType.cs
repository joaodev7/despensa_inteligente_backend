namespace DespensaInteligente.Application.InvoiceScanner.Common;

public enum ErrorType
{
    Validation = 1,
    NotFound = 2,
    UnsupportedState = 3,
    CommunicationError = 4,
    HtmlParsingError = 5,
    Timeout = 6,
    InvalidQrCode = 7,
    Unexpected = 8
}
