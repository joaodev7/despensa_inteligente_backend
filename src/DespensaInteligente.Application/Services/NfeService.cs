using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DespensaInteligente.Application.Common.DTOs;
using DespensaInteligente.Application.Common.Interfaces;
using DespensaInteligente.Application.Interfaces;
using DespensaInteligente.Domain.Entities;

namespace DespensaInteligente.Application.Services
{
    public interface INfeService
    {
        Task<InvoiceExtractionResult> ConsultarEExtrairAsync(string? url, string? chave);
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

        public async Task<InvoiceExtractionResult> ConsultarEExtrairAsync(string? url, string? chave)
        {
            // Query SEFAZ for raw HTML/XML content
            string rawContent = await _sefazService.ConsultarNfeAsync(url, chave);

            // Pass raw XML/HTML contents to the LLM to get structured DTO
            var extraction = await _llmService.ExtractInvoiceAsync(rawContent);

            return extraction;
        }

        public async Task<NfeUploadResultDto> UploadEExtrairAsync(string fileName, Stream fileStream)
        {
            var userId = _currentUserService.UserId ?? throw new UnauthorizedAccessException("Usuário não autenticado.");

            // 1. Save uploaded file to filesystem storage
            string relativePath = await _storageService.SaveFileAsync(userId.ToString(), fileName, fileStream);

            string extension = Path.GetExtension(fileName).ToLower();
            InvoiceExtractionResult extraction;

            if (extension == ".xml" || extension == ".html" || extension == ".htm" || extension == ".txt")
            {
                fileStream.Position = 0;
                using var reader = new StreamReader(fileStream);
                string fileContent = await reader.ReadToEndAsync();
                extraction = await _llmService.ExtractInvoiceAsync(fileContent);
            }
            else
            {
                // PDF or Image upload - pass the stream directly to LLM for native multimodal extraction
                string contentType = extension switch
                {
                    ".pdf" => "application/pdf",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };

                fileStream.Position = 0;
                extraction = await _llmService.ExtractInvoiceAsync("Extraia os itens desta nota fiscal.", fileStream, contentType);
            }

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
                Mercado = input.Extracao.Estabelecimento,
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
                Mercado = input.Extracao.Estabelecimento,
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
                    var itemId = await _compraService.GetOrCreateItemFromCatalogAsync(userId, itemExtracted.Descricao, itemExtracted.Unidade);

                    // Create CompraItem
                    var compraItem = new CompraItem
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        CompraId = compra.Id,
                        ItemId = itemId,
                        NomeOriginal = itemExtracted.Descricao,
                        Quantidade = itemExtracted.Quantidade,
                        Unidade = itemExtracted.Unidade,
                        PrecoUnitario = itemExtracted.ValorUnitario,
                        PrecoTotal = itemExtracted.ValorTotal
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
                        Validade = null,
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
    }

    public class NfeUploadResultDto
    {
        public InvoiceExtractionResult Extracao { get; }
        public string ArquivoNome { get; }
        public string ArquivoPath { get; }

        public NfeUploadResultDto(InvoiceExtractionResult extracao, string arquivoNome, string arquivoPath)
        {
            Extracao = extracao;
            ArquivoNome = arquivoNome;
            ArquivoPath = arquivoPath;
        }
    }

    public class NfeImportDto
    {
        public InvoiceExtractionResult Extracao { get; set; } = new();
        public string? ArquivoNome { get; set; }
        public string? ArquivoPath { get; set; }
    }
}
