using System.Globalization;
using System.Net;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DespensaInteligente.Domain.InvoiceScanner.Entities;
using DespensaInteligente.Domain.InvoiceScanner.ValueObjects;

namespace DespensaInteligente.Infrastructure.InvoiceScanner.Parsers;

/// <summary>
/// Parser especializado e de alta performance para o layout HTML real da SEFAZ Ceará (SEFAZ-CE).
/// Mapeamento estrito da estrutura DOM oficial: table#tabResult > tbody > tr.
/// Sem utilização de Regex.
/// </summary>
public class SefazCeHtmlParser : ISefazCeHtmlParser
{
    private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");
    private readonly ILogger<SefazCeHtmlParser> _logger;

    public SefazCeHtmlParser(ILogger<SefazCeHtmlParser>? logger = null)
    {
        _logger = logger ?? NullLogger<SefazCeHtmlParser>.Instance;
    }

    public Invoice Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            _logger.LogError("Tentativa de parsing com HTML vazio.");
            throw new InvalidOperationException("O HTML fornecido para parsing está vazio.");
        }

        _logger.LogInformation("Iniciando parsing da NFC-e SEFAZ-CE.");

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        ValidateInvoiceExists(doc);

        var storeName = ExtractStoreName(doc);
        var cnpj = ExtractCnpj(doc);
        var accessKey = ExtractAccessKey(doc);
        var issueDate = ExtractIssueDate(doc);
        var paymentMethod = ExtractPaymentMethod(doc);
        var total = ExtractTotalValue(doc);
        var consumer = ExtractConsumer(doc);
        var items = ExtractItems(doc);

        var invoiceTotal = total > 0 ? total : items.Sum(i => i.Total);

        _logger.LogInformation("Parsing da NFC-e SEFAZ-CE concluído com sucesso. Loja: {StoreName}, CNPJ: {Cnpj}, Total: {Total}, Itens: {Count}",
            storeName, cnpj, invoiceTotal, items.Count);

        return new Invoice(
            storeName: storeName,
            cnpj: cnpj,
            issueDate: issueDate,
            accessKey: accessKey,
            total: invoiceTotal,
            paymentMethod: paymentMethod,
            consumer: consumer,
            items: items
        );
    }

    private void ValidateInvoiceExists(HtmlDocument doc)
    {
        var errorNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'alert') or @id='divErros'] | //span[contains(text(), 'não encontrada')]");
        if (errorNode != null)
        {
            var text = CleanText(errorNode.InnerText);
            if (text.Contains("não encontrada", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("inexistente", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Portal SEFAZ-CE indicou que a nota fiscal não existe.");
                throw new InvalidOperationException("Nota fiscal não encontrada no sistema da SEFAZ CE.");
            }
        }
    }

    private static string ExtractStoreName(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//*[@id='txtNomeFantasia']")
                   ?? doc.DocumentNode.SelectSingleNode("//*[@id='txtRazaoSocial']")
                   ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'txtTopo')]");

        var storeName = node != null ? CleanText(node.InnerText) : string.Empty;
        return string.IsNullOrWhiteSpace(storeName) ? "ESTABELECIMENTO SEFAZ-CE" : storeName;
    }

    private static string ExtractCnpj(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//*[@id='txtCnpj']")
                   ?? doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'txtCNPJ')]");

        if (node == null) return string.Empty;

        var digits = ExtractDigits(node.InnerText);
        return digits.Length == 14 ? FormatCnpj(digits) : digits;
    }

    private static string ExtractAccessKey(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//*[@id='chave']")
                   ?? doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'spanChave')]");

        if (node != null)
        {
            var digits = ExtractDigits(node.InnerText);
            if (digits.Length == 44) return digits;
        }

        var nodes = doc.DocumentNode.SelectNodes("//span | //td | //div");
        if (nodes != null)
        {
            foreach (var n in nodes)
            {
                var d = ExtractDigits(n.InnerText);
                if (d.Length == 44) return d;
            }
        }

        return string.Empty;
    }

    private static DateTime ExtractIssueDate(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//*[@id='txtDataEmissao']")
                   ?? doc.DocumentNode.SelectSingleNode("//*[contains(text(), 'Emissão')]");

        if (node != null)
        {
            var text = CleanText(node.InnerText);
            foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (DateTime.TryParse(token, PtBrCulture, DateTimeStyles.None, out var date))
                {
                    return date;
                }
            }
        }

        return DateTime.UtcNow;
    }

    private static string ExtractPaymentMethod(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//*[@id='txtFormaPagamento']")
                   ?? doc.DocumentNode.SelectSingleNode("//td[contains(text(), 'Forma de pagamento')]/following-sibling::td");

        var text = node != null ? CleanText(node.InnerText) : string.Empty;
        return string.IsNullOrWhiteSpace(text) ? "Não Informado" : text;
    }

    private static decimal ExtractTotalValue(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//*[@id='txtTotal']")
                   ?? doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'totalNf')]//span[contains(@class, 'txtMax')]");

        if (node != null && TryParseDecimal(node.InnerText, out var val))
        {
            return val;
        }

        return 0m;
    }

    private static Consumer ExtractConsumer(HtmlDocument doc)
    {
        string? cpf = null;
        string? name = null;
        string? address = null;

        var cpfNode = doc.DocumentNode.SelectSingleNode("//*[@id='txtCpf']");
        if (cpfNode != null)
        {
            var digits = ExtractDigits(cpfNode.InnerText);
            if (digits.Length == 11) cpf = digits;
        }

        var nameNode = doc.DocumentNode.SelectSingleNode("//*[@id='txtNomeConsumer']");
        if (nameNode != null)
        {
            name = CleanText(nameNode.InnerText);
        }

        var addrNode = doc.DocumentNode.SelectSingleNode("//*[@id='txtEnderecoConsumer']");
        if (addrNode != null)
        {
            address = CleanText(addrNode.InnerText);
        }

        return new Consumer(cpf, name, address);
    }

    private List<InvoiceItem> ExtractItems(HtmlDocument doc)
    {
        var items = new List<InvoiceItem>();

        // Mapeamento estrito do layout oficial SEFAZ-CE: table#tabResult > tbody > tr
        var itemRows = doc.DocumentNode.SelectNodes("//table[@id='tabResult']//tr");

        if (itemRows == null || itemRows.Count == 0)
        {
            LogDiagnosticsAndThrow(doc);
        }

        _logger.LogInformation("Tabela de produtos encontrada. Quantidade de linhas encontradas: {Count}", itemRows!.Count);

        foreach (var row in itemRows)
        {
            var cells = row.SelectNodes("./td");
            if (cells == null || cells.Count < 2) continue;

            var firstCell = cells[0];
            var secondCell = cells[1];

            // 1. Descrição (span.txtTit)
            var descNode = firstCell.SelectSingleNode("./span[contains(@class, 'txtTit')]");
            if (descNode == null) continue;

            var description = CleanText(descNode.InnerText);
            if (string.IsNullOrWhiteSpace(description) || description.Equals("Descrição", StringComparison.OrdinalIgnoreCase))
                continue;

            // 2. Código (span.RCod) - Ex: (Código:401534)
            var codeNode = firstCell.SelectSingleNode("./span[contains(@class, 'RCod')]");
            var code = codeNode != null ? ParseCode(codeNode.InnerText) : string.Empty;

            // 3. Quantidade (span.Rqtd) - Ex: Qtde.:1,965
            var qtdNode = firstCell.SelectSingleNode("./span[contains(@class, 'Rqtd')]")
                          ?? firstCell.SelectSingleNode("./span[contains(@class, 'Rqty')]");
            var quantity = qtdNode != null ? ParseValueAfterPrefix(qtdNode.InnerText, "Qtde.:", "Qtde:", "Qtd.:") : 1m;

            // 4. Unidade (span.RUN) - Ex: UN:KG
            var unitNode = firstCell.SelectSingleNode("./span[contains(@class, 'RUN')]");
            var unit = unitNode != null ? ParseUnit(unitNode.InnerText) : "UN";

            // 5. Preço Unitário (span.RvlUnit) - Ex: Vl. Unit.:17,99
            var priceNode = firstCell.SelectSingleNode("./span[contains(@class, 'RvlUnit')]");
            var unitPrice = priceNode != null ? ParseValueAfterPrefix(priceNode.InnerText, "Vl. Unit.:", "Vl.Unit.:", "Vl. Unit:") : 0m;

            // 6. Valor Total (td[2]) - Ex: <td class="txtTit noWrap" align="right">35,35</td>
            var total = TryParseDecimal(secondCell.InnerText, out var totalVal) ? totalVal : Math.Round(quantity * unitPrice, 2);

            // Validações
            if (quantity <= 0) quantity = 1m;

            if (unitPrice <= 0 && total > 0)
            {
                unitPrice = Math.Round(total / quantity, 4);
            }

            if (total <= 0 && unitPrice > 0)
            {
                total = Math.Round(quantity * unitPrice, 2);
            }

            _logger.LogInformation("Produto encontrado: {Description} | Quantidade: {Quantity} | Preço Unitário: {UnitPrice} | Valor Total: {Total}",
                description, quantity, unitPrice, total);

            items.Add(new InvoiceItem(description, code, quantity, unit, unitPrice, total));
        }

        if (items.Count == 0)
        {
            LogDiagnosticsAndThrow(doc);
        }

        return items;
    }

    private void LogDiagnosticsAndThrow(HtmlDocument doc)
    {
        var tablesCount = doc.DocumentNode.SelectNodes("//table")?.Count ?? 0;
        var rowsCount = doc.DocumentNode.SelectNodes("//tr")?.Count ?? 0;
        var txtTitCount = doc.DocumentNode.SelectNodes("//span[contains(@class, 'txtTit')]")?.Count ?? 0;
        var rCodCount = doc.DocumentNode.SelectNodes("//span[contains(@class, 'RCod')]")?.Count ?? 0;
        var rQtdCount = doc.DocumentNode.SelectNodes("//span[contains(@class, 'Rqtd')]")?.Count ?? 0;
        var rUnCount = doc.DocumentNode.SelectNodes("//span[contains(@class, 'RUN')]")?.Count ?? 0;
        var rVlUnitCount = doc.DocumentNode.SelectNodes("//span[contains(@class, 'RvlUnit')]")?.Count ?? 0;

        _logger.LogError("Nenhum produto foi encontrado no HTML da SEFAZ-CE. Estatísticas do DOM: " +
                         "quantidade de <table>: {TablesCount}, " +
                         "quantidade de <tr>: {RowsCount}, " +
                         "quantidade de <span class=\"txtTit\">: {TxtTitCount}, " +
                         "quantidade de <span class=\"RCod\">: {RCodCount}, " +
                         "quantidade de <span class=\"Rqtd\">: {RQtdCount}, " +
                         "quantidade de <span class=\"RUN\">: {RUnCount}, " +
                         "quantidade de <span class=\"RvlUnit\">: {RVlUnitCount}",
            tablesCount, rowsCount, txtTitCount, rCodCount, rQtdCount, rUnCount, rVlUnitCount);

        throw new InvalidOperationException("Nenhum item foi encontrado no HTML da nota fiscal da SEFAZ CE.");
    }

    private static string ParseCode(string text)
    {
        var cleaned = CleanText(text);
        var idx = cleaned.IndexOf("Código:", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var portion = cleaned[(idx + 7)..].Trim();
            var endIdx = portion.IndexOf(')');
            if (endIdx >= 0) return portion[..endIdx].Trim();
            return portion.Split(' ')[0].Trim();
        }
        return ExtractDigits(cleaned);
    }

    private static string ParseUnit(string text)
    {
        var cleaned = CleanText(text);
        var idx = cleaned.IndexOf("UN:", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var portion = cleaned[(idx + 3)..].Trim();
            return portion.Split(' ')[0].Trim();
        }
        return "UN";
    }

    private static decimal ParseValueAfterPrefix(string text, params string[] prefixes)
    {
        var cleaned = CleanText(text);
        foreach (var prefix in prefixes)
        {
            var idx = cleaned.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var portion = cleaned[(idx + prefix.Length)..].Trim();
                var firstToken = portion.Split(' ')[0].Trim();
                if (TryParseDecimal(firstToken, out var val))
                {
                    return val;
                }
            }
        }

        if (TryParseDecimal(cleaned, out var directVal))
        {
            return directVal;
        }

        return 0m;
    }

    private static bool TryParseDecimal(string input, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var cleaned = CleanText(input);
        var sb = new System.Text.StringBuilder();

        foreach (var ch in cleaned)
        {
            if (char.IsDigit(ch) || ch == ',' || ch == '.')
            {
                sb.Append(ch);
            }
        }

        var str = sb.ToString();
        if (str.Contains('.') && str.Contains(','))
        {
            str = str.Replace(".", "");
        }

        return decimal.TryParse(str, PtBrCulture, out result);
    }

    private static string CleanText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var decoded = WebUtility.HtmlDecode(input);
        return string.Join(" ", decoded.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static string ExtractDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return new string(input.Where(char.IsDigit).ToArray());
    }

    private static string FormatCnpj(string digits)
    {
        if (digits.Length != 14) return digits;
        return $"{digits[..2]}.{digits.Substring(2, 3)}.{digits.Substring(5, 3)}/{digits.Substring(8, 4)}-{digits[12..]}";
    }
}
