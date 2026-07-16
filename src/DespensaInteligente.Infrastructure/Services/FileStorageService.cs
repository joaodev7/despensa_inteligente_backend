using System;
using System.IO;
using System.Threading.Tasks;
using DespensaInteligente.Application.Common.Interfaces;

namespace DespensaInteligente.Infrastructure.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly string _storageRoot;

        public FileStorageService()
        {
            // Root storage folder relative to directory execution
            _storageRoot = Path.Combine(Directory.GetCurrentDirectory(), "storage");
        }

        public async Task<string> SaveFileAsync(string userId, string fileName, Stream fileStream)
        {
            var userDir = Path.Combine(_storageRoot, "notas-fiscais", userId);
            if (!Directory.Exists(userDir))
            {
                Directory.CreateDirectory(userDir);
            }

            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
            var fullPath = Path.Combine(userDir, uniqueFileName);

            using var destinationStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await fileStream.CopyToAsync(destinationStream);

            // Return path relative to the execution root (for persistence and retrieving)
            return Path.Combine("storage", "notas-fiscais", userId, uniqueFileName);
        }

        public void DeleteFile(string filePath)
        {
            // Translate relative path back to full path
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}
