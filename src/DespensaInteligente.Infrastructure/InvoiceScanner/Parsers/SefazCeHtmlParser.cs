using System.Globalization;
using System.Net;
using HtmlAgilityPack;
using DespensaInteligente.Domain.InvoiceScanner.Entities;
using DespensaInteligente.Domain.InvoiceScanner.ValueObjects;

namespace DespensaInteligente.Infrastructure.InvoiceScanner.Parsers;

/// <summary>
/// Parser especializado para converter a página HTML da NFC-e da SEFAZ Ceará (SEFAZ-CE) na entidade Invoice.
/// Utiliza HtmlAgilityPack e manipulação segura de strings sem utilizar Expressões Regulares (Regex).
/// </summary>
public class SefazCeHtmlParser : ISefazCeHtmlParser
{
    private static readonly CultureInfo PtBrCulture = new CultureInfo("pt-BR");

    public Invoice Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new InvalidOperationException("O HTML fornecido para parsing está vazio.");
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Verificar se a nota fiscal não foi encontrada ou portal retornou mensagem de erro
        if (IsInvoiceNotFound(doc))
        {
            throw new InvalidOperationException("Nota fiscal não encontrada no sistema da SEFAZ CE.");
        }

        var storeName = ExtractStoreName(doc);
        var cnpj = ExtractCnpj(doc);
        var accessKey = ExtractAccessKey(doc);
        var issueDate = ExtractIssueDate(doc);
        var paymentMethod = ExtractPaymentMethod(doc);
        var total = ExtractTotalValue(doc);
        var consumer = ExtractConsumer(doc);
        var items = ExtractItems(doc);

        if (items.Count == 0)
        {
            throw new InvalidOperationException("Nenhum item foi encontrado no HTML da nota fiscal da SEFAZ CE. O layout pode ter sido alterado.");
        }

        return new Invoice(
            storeName: storeName,
            cnpj: cnpj,
            issueDate: issueDate,
            accessKey: accessKey,
            total: total > 0 ? total : items.Sum(i => i.Total),
            paymentMethod: paymentMethod,
            consumer: consumer,
            items: items
        );
    }

    private static bool IsInvoiceNotFound(HtmlDocument doc)
    {
        var errorNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'alert') or contains(@class, 'erro') or @id='divErros']")
                        ?? doc.DocumentNode.SelectSingleNode("//span[contains(text(), 'não encontrada') or contains(text(), 'Nao Encontrada')]");

        if (errorNode != null)
        {
            var text = WebUtility.HtmlDecode(errorNode.InnerText);
            if (text.Contains("não encontrada", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("inexistente", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string ExtractStoreName(HtmlDocument doc)
    {
        // Tenta seletores comuns da SEFAZ CE (#txtNomeFantasia, .txtTopo, #u20, .txtCenter)
        var node = doc.DocumentNode.SelectSingleNode("//*[@id='txtNomeFantasia']")
                   ?? doc.DocumentNode.SelectSingleNode("//*[@id='txtRazaoSocial']")
                   ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'txtTopo')]")
                   ?? doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'head')]//td[contains(@class, 'txtTopo')]")
                   ?? doc.DocumentNode.SelectSingleNode("//*[@id='u20']")
                   ?? doc.DocumentNode.SelectSingleNode("//div[contains(@id, 'conteudo')]//div[contains(@class, 'txtCenter')][1]");

        if (node != null)
        {
            var text = CleanText(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        return "ESTABELECIMENTO SEFAZ-CE";
    }

    private static string ExtractCnpj(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//*[@id='txtCnpj']")
                   ?? doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'txtCNPJ')]")
                   ?? doc.DocumentNode.SelectSingleNode("//*[contains(text(), 'CNPJ')]");

        if (node != null)
        {
            var digits = ExtractDigits(node.InnerText);
            if (digits.Length == 14)
            {
                return ConvertToCnpjFormat(digits);
            }
            if (digits.Length > 0)
            {
                return digits;
            }
        }

        return string.Empty;
    }

    private static string ExtractAccessKey(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//*[@id='chave']")
                   ?? doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'chave')]")
                   ?? doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'spanChave')]")
                   ?? doc.DocumentNode.SelectSingleNode("//*[@id='nfe-chave']");

        if (node != null)
        {
            var digits = ExtractDigits(node.InnerText);
            if (digits.Length == 44)
            {
                return digits;
            }
        }

        // Procura nó de texto com 44 dígitos sem regex
        var allSpans = doc.DocumentNode.SelectNodes("//span | //td | //div");
        if (allSpans != null)
        {
            foreach (var span in allSpans)
            {
                var digits = ExtractDigits(span.InnerText);
                if (digits.Length == 44)
                {
                    return digits;
                }
            }
        }

        return string.Empty;
    }

    private static DateTime ExtractIssueDate(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode.SelectNodes("//*[contains(text(), 'Emissão') or contains(text(), 'Emissao') or @id='txtDataEmissao']");
        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                var text = CleanText(node.InnerText);
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (DateTime.TryParse(part, PtBrCulture, DateTimeStyles.None, out var date))
                    {
                        return date;
                    }
                }
            }
        }

        return DateTime.UtcNow;
    }

    private static string ExtractPaymentMethod(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//*[@id='txtFormaPagamento']")
                   ?? doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'formaPagamento')]")
                   ?? doc.DocumentNode.SelectSingleNode("//td[contains(text(), 'Forma de pagamento')]/following-sibling::td")
                   ?? doc.DocumentNode.SelectSingleNode("//span[contains(text(), 'Cartão') or contains(text(), 'Dinheiro') or contains(text(), 'Pix')]");

        if (node != null)
        {
            var text = CleanText(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        return "Outros / Não informado";
    }

    private static decimal ExtractTotalValue(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//*[@id='txtTotal']")
                   ?? doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'totalNf')]//span[contains(@class, 'txtMax')]")
                   ?? doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'totalNf')]")
                   ?? doc.DocumentNode.SelectSingleNode("//td[contains(text(), 'Valor a pagar')]/following-sibling::td")
                   ?? doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'txtMax')]");

        if (node != null)
        {
            if (TryParseDecimal(node.InnerText, out var val))
            {
                return val;
            }
        }

        return 0m;
    }

    private static Consumer ExtractConsumer(HtmlDocument doc)
    {
        string? cpf = null;
        string? name = null;
        string? address = null;

        var cpfNode = doc.DocumentNode.SelectSingleNode("//*[@id='txtCpf']")
                      ?? doc.DocumentNode.SelectSingleNode("//*[contains(text(), 'CPF do Consumidor')]");
        if (cpfNode != null)
        {
            var digits = ExtractDigits(cpfNode.InnerText);
            if (digits.Length == 11)
            {
                cpf = digits;
            }
        }

        var nameNode = doc.DocumentNode.SelectSingleNode("//*[@id='txtNomeConsumer']")
                       ?? doc.DocumentNode.SelectSingleNode("//*[contains(text(), 'Nome do Consumidor')]");
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

    private static List<InvoiceItem> ExtractItems(HtmlDocument doc)
    {
        var items = new List<InvoiceItem>();

        // SEFAZ CE costuma listar itens na tabela #tabResult ou em trs com id ou classe contendo 'Item'
        var itemRows = doc.DocumentNode.SelectNodes("//table[@id='tabResult']//tr[starts-with(@id, 'Item')]")
                       ?? doc.DocumentNode.SelectNodes("//table[@id='tabResult']//tr")
                       ?? doc.DocumentNode.SelectNodes("//tr[contains(@class, 'trItem')]")
                       ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'linhaItem')]");

        if (itemRows == null)
        {
            return items;
        }

        foreach (var row in itemRows)
        {
            // Pular cabeçalho
            if (row.SelectSingleNode("./th") != null) continue;

            var descNode = row.SelectSingleNode(".//*[contains(@class, 'txtTit')]")
                           ?? row.SelectSingleNode(".//*[contains(@class, 'spanItem')]")
                           ?? row.SelectSingleNode(".//td[1]");

            if (descNode == null) continue;

            var description = CleanText(descNode.InnerText);
            if (string.IsNullOrWhiteSpace(description) || description.Equals("Descrição", StringComparison.OrdinalIgnoreCase))
                continue;

            // Extrair Código (Código: (xxx))
            var codeNode = row.SelectSingleNode(".//*[contains(@class, 'RCod')]")
                           ?? row.SelectSingleNode(".//*[contains(@class, 'spanCod')]")
                           ?? row.SelectSingleNode(".//*[contains(text(), 'Código')]");
            var code = codeNode != null ? ExtractCodeValue(codeNode.InnerText) : string.Empty;

            // Extrair Quantidade
            var qtyNode = row.SelectSingleNode(".//*[contains(@class, 'Rqty')]")
                          ?? row.SelectSingleNode(".//*[contains(@class, 'spanQty')]")
                          ?? row.SelectSingleNode(".//*[contains(text(), 'Qtde')]");
            var quantity = qtyNode != null ? ExtractDecimalValueAfterPrefix(qtyNode.InnerText, "Qtde.:", "Qtd.:", "Qtde:") : 1m;

            // Extrair Unidade (UN)
            var unitNode = row.SelectSingleNode(".//*[contains(@class, 'RUN')]")
                           ?? row.SelectSingleNode(".//*[contains(@class, 'spanUn')]")
                           ?? row.SelectSingleNode(".//*[contains(text(), 'UN:')]");
            var unit = unitNode != null ? ExtractUnitValue(unitNode.InnerText) : "UN";

            // Extrair Valor Unitário
            var priceNode = row.SelectSingleNode(".//*[contains(@class, 'RvlUnit')]")
                            ?? row.SelectSingleNode(".//*[contains(@class, 'spanVlUnit')]")
                            ?? row.SelectSingleNode(".//*[contains(text(), 'Vl. Unit.')]");
            var unitPrice = priceNode != null ? ExtractDecimalValueAfterPrefix(priceNode.InnerText, "Vl. Unit.:", "Vl.Unit.:", "Vl. Unit:") : 0m;

            // Extrair Valor Total
            var totalNode = row.SelectSingleNode(".//*[contains(@class, 'valor')]")
                            ?? row.SelectSingleNode(".//*[contains(@class, 'spanVlTotal')]")
                            ?? row.SelectSingleNode(".//td[last()]");
            var itemTotal = totalNode != null && TryParseDecimal(totalNode.InnerText, out var totVal) ? totVal : Math.Round(quantity * unitPrice, 2);

            if (unitPrice == 0m && itemTotal > 0 && quantity > 0)
            {
                unitPrice = Math.Round(itemTotal / quantity, 4);
            }

            items.Add(new InvoiceItem(description, code, quantity, unit, unitPrice, itemTotal));
        }

        return items;
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

    private static string ConvertToCnpjFormat(string digits)
    {
        if (digits.Length != 14) return digits;
        return $"{digits[..2]}.{digits.Substring(2, 3)}.{digits.Substring(5, 3)}/{digits.Substring(8, 4)}-{digits[12..]}";
    }

    private static string ExtractCodeValue(string text)
    {
        var cleaned = CleanText(text);
        var idx = cleaned.IndexOf("Código:", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var substring = cleaned[(idx + 7)..].Trim();
            var endIdx = substring.IndexOf(')');
            if (endIdx > 0) return substring[..endIdx].Replace("(", "").Trim();
            return substring.Split(' ')[0];
        }
        return ExtractDigits(cleaned);
    }

    private static string ExtractUnitValue(string text)
    {
        var cleaned = CleanText(text);
        var idx = cleaned.IndexOf("UN:", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var substring = cleaned[(idx + 3)..].Trim();
            return substring.Split(' ')[0];
        }
        return "UN";
    }

    private static decimal ExtractDecimalValueAfterPrefix(string text, params string[] prefixes)
    {
        var cleaned = CleanText(text);
        foreach (var prefix in prefixes)
        {
            var idx = cleaned.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var portion = cleaned[(idx + prefix.Length)..].Trim();
                var firstToken = portion.Split(' ')[0];
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

        var clean = ExtractDigitsAndSeparator(input);
        return decimal.TryParse(clean, PtBrCulture, out result);
    }

    private static string ExtractDigitsAndSeparator(string input)
    {
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
        // Se houver vírgula e ponto, assumir formato BR (1.234,56) -> remover pontos e trocar vírgula por ponto no culture
        if (str.Contains('.') && str.Contains(','))
        {
            str = str.Replace(".", "");
        }

        return str;
    }
}
