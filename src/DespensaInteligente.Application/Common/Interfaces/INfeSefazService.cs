using System.Threading.Tasks;

namespace DespensaInteligente.Application.Common.Interfaces
{
    public interface INfeSefazService
    {
        Task<string> ConsultarNfeAsync(string? url, string? chave);
    }
}
