using System.Globalization;
using System.Net;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DespensaInteligente.Infrastructure.InvoiceScanner.Models;

namespace DespensaInteligente.Infrastructure.InvoiceScanner.Parsers;

/// <summary>
/// Parser de URLs de QRCode da SEFAZ-CE.
/// Reproduz a lógica do app.js oficial da SEFAZ-CE para extração de QRCode v2 (NFC-e modelo 65):
/// Índice 0 = chave_acesso
/// Índice 1 = versao_qrcode
/// Índice 2 = tipo_ambiente
/// Índice 3 = identificador_csc
/// Índice 4 = codigo_hash
/// </summary>
public class SefazCeQrCodeParser : ISefazCeQrCodeParser
{
    private readonly ILogger<SefazCeQrCodeParser> _logger;

    public SefazCeQrCodeParser(ILogger<SefazCeQrCodeParser>? logger = null)
    {
        _logger = logger ?? NullLogger<SefazCeQrCodeParser>.Instance;
    }

    public SefazCeQrCodePayload Parse(string qrCodeUrl)
    {
        _logger.LogInformation("QRCode recebido para análise: {QrCodeUrl}", qrCodeUrl);

        if (string.IsNullOrWhiteSpace(qrCodeUrl))
        {
            throw new ArgumentException("A URL do QRCode não pode ser nula nem vazia.", nameof(qrCodeUrl));
        }

        // 1. Decodificar URL completa (UrlDecode)
        var decodedUrl = WebUtility.UrlDecode(qrCodeUrl);

        // 2. Extrair parâmetro 'p'
        var pParam = ExtractQueryParam(decodedUrl, "p");

        if (string.IsNullOrWhiteSpace(pParam))
        {
            var pIndex = decodedUrl.IndexOf("p=", StringComparison.OrdinalIgnoreCase);
            if (pIndex >= 0)
            {
                pParam = decodedUrl[(pIndex + 2)..];
                var ampersandIndex = pParam.IndexOf('&');
                if (ampersandIndex >= 0)
                {
                    pParam = pParam[..ampersandIndex];
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(pParam) && pParam.Contains('|'))
        {
            var decodedPParam = WebUtility.UrlDecode(pParam);
            return ParsePipeSeparatedPayload(decodedPParam);
        }

        return ParseQueryStringPayload(decodedUrl);
    }

    private SefazCeQrCodePayload ParsePipeSeparatedPayload(string decodedPParam)
    {
        var parametros = decodedPParam.Split('|');

        _logger.LogInformation("Parametros extraídos:");
        for (var i = 0; i < parametros.Length; i++)
        {
            _logger.LogInformation("[{Index}] = {Valor}", i, parametros[i]);
        }

        // Mapeamento oficial do app.js da SEFAZ-CE para NFC-e Modelo 65 v2:
        // Index 0: chave_acesso
        // Index 1: versao_qrcode
        // Index 2: tipo_ambiente
        // Index 3: identificador_csc
        // Index 4: codigo_hash
        var chaveAcesso = ExtractDigits(parametros.Length > 0 ? parametros[0] : string.Empty);
        
        int versao = 2;
        if (parametros.Length > 1 && int.TryParse(parametros[1], out var parsedVersao))
        {
            versao = parsedVersao;
        }

        int ambiente = 1;
        if (parametros.Length > 2 && int.TryParse(parametros[2], out var parsedAmbiente))
        {
            ambiente = parsedAmbiente;
        }

        string identificadorCSC = string.Empty;
        string codigoHash = string.Empty;

        if (parametros.Length >= 5)
        {
            // Mapeamento v2 oficial SEFAZ-CE:
            // parametros[3] = identificador_csc (ex: "2" ou "000002")
            // parametros[4] = codigo_hash
            identificadorCSC = CleanParam(parametros[3]);
            codigoHash = CleanParam(parametros[4]);
        }
        else if (parametros.Length > 3)
        {
            identificadorCSC = CleanParam(parametros[3]);
        }

        var payload = new SefazCeQrCodePayload
        {
            ChaveAcesso = chaveAcesso,
            VersaoQrCode = versao,
            TipoAmbiente = ambiente,
            IdentificadorCSC = string.IsNullOrWhiteSpace(identificadorCSC) ? "2" : identificadorCSC,
            CodigoHash = codigoHash
        };

        _logger.LogInformation("[PAYLOAD FINAL GERADO] ChaveAcesso: {ChaveAcesso}, VersaoQrCode: {VersaoQrCode}, TipoAmbiente: {TipoAmbiente}, IdentificadorCSC: {IdentificadorCSC}, CodigoHash: {CodigoHash}",
            payload.ChaveAcesso, payload.VersaoQrCode, payload.TipoAmbiente, payload.IdentificadorCSC, payload.CodigoHash);

        return payload;
    }

    private SefazCeQrCodePayload ParseQueryStringPayload(string decodedUrl)
    {
        var chave = ExtractQueryParam(decodedUrl, "chNFe") ?? ExtractQueryParam(decodedUrl, "chave") ?? Extract44Digits(decodedUrl);
        var versaoStr = ExtractQueryParam(decodedUrl, "nVersao") ?? ExtractQueryParam(decodedUrl, "v") ?? "2";
        var ambStr = ExtractQueryParam(decodedUrl, "tpAmb") ?? "1";
        var csc = ExtractQueryParam(decodedUrl, "cIdToken") ?? ExtractQueryParam(decodedUrl, "csc") ?? "2";
        var hash = ExtractQueryParam(decodedUrl, "cHashQRCode") ?? ExtractQueryParam(decodedUrl, "hash") ?? string.Empty;

        int.TryParse(versaoStr, out var versao);
        if (versao == 0) versao = 2;

        int.TryParse(ambStr, out var ambiente);
        if (ambiente == 0) ambiente = 1;

        var payload = new SefazCeQrCodePayload
        {
            ChaveAcesso = chave,
            VersaoQrCode = versao,
            TipoAmbiente = ambiente,
            IdentificadorCSC = csc,
            CodigoHash = hash
        };

        _logger.LogInformation("[PAYLOAD FINAL GERADO VIA QUERYSTRING] ChaveAcesso: {ChaveAcesso}, VersaoQrCode: {VersaoQrCode}, TipoAmbiente: {TipoAmbiente}, IdentificadorCSC: {IdentificadorCSC}, CodigoHash: {CodigoHash}",
            payload.ChaveAcesso, payload.VersaoQrCode, payload.TipoAmbiente, payload.IdentificadorCSC, payload.CodigoHash);

        return payload;
    }

    private static string CleanParam(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return WebUtility.UrlDecode(value).Trim();
    }

    private static string? ExtractQueryParam(string url, string paramName)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var questionIdx = url.IndexOf('?');
                if (questionIdx >= 0)
                {
                    url = "http://nfce.sefaz.ce.gov.br" + url[questionIdx..];
                    Uri.TryCreate(url, UriKind.Absolute, out uri);
                }
            }

            if (uri != null)
            {
                var queryParams = HttpUtility.ParseQueryString(uri.Query);
                return queryParams[paramName];
            }
        }
        catch
        {
            // Ignora erro de parse secundário
        }
        return null;
    }

    private static string Extract44Digits(string input)
    {
        var digits = ExtractDigits(input);
        if (digits.Length >= 44)
        {
            return digits[..44];
        }
        return digits;
    }

    private static string ExtractDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return new string(input.Where(char.IsDigit).ToArray());
    }
}
