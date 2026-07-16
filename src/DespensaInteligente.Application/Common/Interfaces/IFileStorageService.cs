using System.IO;
using System.Threading.Tasks;

namespace DespensaInteligente.Application.Common.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(string userId, string fileName, Stream fileStream);
        void DeleteFile(string filePath);
    }
}
