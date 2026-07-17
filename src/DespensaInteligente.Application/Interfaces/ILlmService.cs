using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DespensaInteligente.Application.Common.DTOs;

namespace DespensaInteligente.Application.Interfaces
{
    public interface ILlmService
    {
        Task<InvoiceExtractionResult> ExtractInvoiceAsync(
            string prompt,
            Stream? file = null,
            string? contentType = null,
            CancellationToken cancellationToken = default);

        Task<string> GenerateAsync(
            string prompt,
            Stream? file = null,
            string? contentType = null,
            CancellationToken cancellationToken = default);
    }
}
