using System.Threading.Tasks;
using DespensaInteligente.Application.Common.DTOs;

namespace DespensaInteligente.Application.Common.Interfaces
{
    public interface ILlmService
    {
        Task<NfeExtractionResultDto> ExtractNfeDataAsync(string rawContent);
    }
}
