using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DespensaInteligente.Application.Common.DTOs;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Domain.Entities;

namespace DespensaInteligente.Application.Services
{
    public interface INfeService
    {
        Task<NfeExtractionResultDto> ConsultarEExtrairAsync(string? url, string? chave);
        Task<NfeUploadResultDto> UploadEExtrairAsync(string fileName, Stream fileStream);
        Task<Compra> ImportarExtracaoAsync(NfeImportDto input);
        Task<IEnumerable<NotaFiscal>> GetHistoricoAsync();
    }

    public class NfeService : INfeService
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly INfeSefazService _sefazService;
        private readonly ILlmService _llmService;
        private readonly IFileStorageService _storageService;
        private readonly ICompraService _compraService;

        public NfeService(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            INfeSefazService sefazService,
            ILlmService llmService,
            IFileStorageService storageService,
            ICompraService compraService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _sefazService = sefazService;
            _llmService = llmService;
            _storageService = storageService;
            _compraService = compraService;
        }

        public async Task<NfeExtractionResultDto> ConsultarEExtrairAsync(string? url, string? chave)
        {
            // Query SEFAZ for raw HTML/XML content
            string rawContent = await _sefazService.ConsultarNfeAsync(url, chave);

            // Pass raw XML/HTML contents to the LLM to get structured DTO
            var extraction = await _llmService.ExtractNfeDataAsync(rawContent);

            return extraction;
        }

        public async Task<NfeUploadResultDto> UploadEExtrairAsync(string fileName, Stream fileStream)
        {
            var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

            // 1. Save uploaded file to filesystem storage
            string relativePath = await _storageService.SaveFileAsync(userId.ToString(), fileName, fileStream);

            // Read contents to feed the LLM
            // Note: If PDF/image, a production solution would extract OCR first. 
            // In our implementation, we read text/XML directly, and if binary (like PDF/image), 
            // we simulate/mock OCR text extraction, or if it is XML/HTML we parse directly.
            string fileContent = "";
            string extension = Path.GetExtension(fileName).ToLower();

            if (extension == ".xml" || extension == ".html" || extension == ".htm" || extension == ".txt")
            {
                fileStream.Position = 0;
                using var reader = new StreamReader(fileStream);
                fileContent = await reader.ReadToEndAsync();
            }
            else
            {
                // PDF or Image upload fallback - simulated text extractor
                fileContent = GetMockOcrContent(fileName);
            }

            // Extract with LLM
            var extraction = await _llmService.ExtractNfeDataAsync(fileContent);

            return new NfeUploadResultDto(
                extraction,
                fileName,
                relativePath
            );
        }

        public async Task<Compra> ImportarExtracaoAsync(NfeImportDto input)
        {
            var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

            var cleanChave = System.Text.RegularExpressions.Regex.Replace(input.Extracao.ChaveAcesso ?? "", @"\D", "");
            
            if (cleanChave.Length == 44)
            {
                // Check if this access key has already been imported
                var alreadyImported = await _context.NotasFiscais
                    .IgnoreQueryFilters() // Enforce database uniqueness check
                    .AnyAsync(nf => nf.UserId == userId && nf.ChaveAcesso == cleanChave);

                if (alreadyImported)
                {
                    throw new InvalidOperationException("Esta NFC-e já foi importada anteriormente para a sua conta.");
                }
            }

            using var transaction = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled);

            // Serialize the raw extraction details to store in PostgreSQL jsonb
            string rawExtracaoJson = JsonSerializer.Serialize(input.Extracao);

            // 1. Create NotaFiscal
            var notaFiscal = new NotaFiscal
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ChaveAcesso = cleanChave,
                Mercado = input.Extracao.Mercado,
                DataCompra = input.Extracao.DataCompra,
                ValorTotal = input.Extracao.ValorTotal,
                ArquivoNome = input.ArquivoNome,
                ArquivoPath = input.ArquivoPath,
                RawExtracao = rawExtracaoJson,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.NotasFiscais.Add(notaFiscal);

            // 2. Create Compra
            var compra = new Compra
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Mercado = input.Extracao.Mercado,
                DataCompra = input.Extracao.DataCompra,
                ValorTotal = input.Extracao.ValorTotal,
                NotaFiscalId = notaFiscal.Id,
                Observacoes = $"Importada via nota fiscal. Chave: {cleanChave}",
                CreatedAt = DateTimeOffset.UtcNow
            };
            _context.Compras.Add(compra);

            // Save basic records first
            await _context.SaveChangesAsync();

            // 3. Process items and create lots
            if (input.Extracao.Itens != null)
            {
                foreach (var itemExtracted in input.Extracao.Itens)
                {
                    // Match with user catalog or create new catalog entry
                    var itemId = await _compraService.GetOrCreateItemFromCatalogAsync(userId, itemExtracted.Nome, itemExtracted.Unidade);

                    // Create CompraItem
                    var compraItem = new CompraItem
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        CompraId = compra.Id,
                        ItemId = itemId,
                        NomeOriginal = itemExtracted.Nome,
                        Quantidade = itemExtracted.Quantidade,
                        Unidade = itemExtracted.Unidade,
                        PrecoUnitario = itemExtracted.PrecoUnitario,
                        PrecoTotal = itemExtracted.PrecoTotal
                    };
                    _context.CompraItens.Add(compraItem);

                    // Create physical Lote (always local='despensa')
                    var lote = new Lote
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        ItemId = itemId,
                        CompraId = compra.Id,
                        Quantidade = itemExtracted.Quantidade,
                        Validade = null, // LLM parsing rarely retrieves expiration dates (usually they aren't in invoice XML/HTML). Users can edit them later.
                        Local = "despensa",
                        Consumido = false,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _context.Lotes.Add(lote);
                }
            }

            await _context.SaveChangesAsync();

            transaction.Complete();

            return compra;
        }

        public async Task<IEnumerable<NotaFiscal>> GetHistoricoAsync()
        {
            return await _context.NotasFiscais
                .OrderByDescending(nf => nf.CreatedAt)
                .ToListAsync();
        }

        private string GetMockOcrContent(string fileName)
        {
            // Simulated OCR parsing output for images/PDFs
            return $@"
            CHAVE DE ACESSO: 35260712345678000190650010001234561001234567
            SUPERMERCADO DIA A DIA LTDA
            CNPJ: 12.345.678/0001-90
            DATA DA COMPRA: {DateTime.Today:dd/MM/yyyy}
            VALOR TOTAL: 87.90
            
            ITENS:
            1. MACARRAO SPAGHETTI 500G - Qtd: 3 - Un: pct - Preco Unit: 3.50 - Total: 10.50
            2. LEITE INTEGRAL UHT 1L - Qtd: 12 - Un: l - Preco Unit: 4.95 - Total: 59.40
            3. AZEITE DE OLIVA EXTRA VIRGEM 500ML - Qtd: 1 - Un: ml - Preco Unit: 18.00 - Total: 18.00
            ";
        }
    }

    public class NfeUploadResultDto
    {
        public NfeExtractionResultDto Extracao { get; }
        public string ArquivoNome { get; }
        public string ArquivoPath { get; }

        public NfeUploadResultDto(NfeExtractionResultDto extracao, string arquivoNome, string arquivoPath)
        {
            Extracao = extracao;
            ArquivoNome = arquivoNome;
            ArquivoPath = arquivoPath;
        }
    }

    public class NfeImportDto
    {
        public NfeExtractionResultDto Extracao { get; set; } = new();
        public string? ArquivoNome { get; set; }
        public string? ArquivoPath { get; set; }
    }
}
