using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using FluentValidation;
using DespensaInteligente.Application.InvoiceScanner.Common;
using DespensaInteligente.Application.InvoiceScanner.DTOs;
using DespensaInteligente.Application.InvoiceScanner.Interfaces;
using DespensaInteligente.Application.InvoiceScanner.Services;
using DespensaInteligente.Application.InvoiceScanner.Validators;
using DespensaInteligente.Infrastructure.InvoiceScanner.Http;
using DespensaInteligente.Infrastructure.InvoiceScanner.Parsers;
using DespensaInteligente.Infrastructure.InvoiceScanner.Providers;
using Xunit;

namespace DespensaInteligente.Tests;

public class InvoiceScannerTests
{
    private readonly SefazCeHtmlParser _parser;

    public InvoiceScannerTests()
    {
        _parser = new SefazCeHtmlParser();
    }

    [Theory]
    [InlineData("https://nfce.sefaz.ce.gov.br/pages/ShowNFCe.html?p=23230706509999000100650010000012341234567890", true)]
    [InlineData("https://servicos.sefaz.ce.gov.br/internet/consultanfce/consulta.asp?p=123", true)]
    [InlineData("https://nfce.sefaz.sp.gov.br/consultanfce", false)]
    [InlineData("https://google.com", false)]
    [InlineData("invalid-url", false)]
    public void SefazCeProvider_CanHandle_ShouldValidateCorrectly(string url, bool expectedResult)
    {
        // Arrange
        var mockHttp = new MockHttpClient("");
        var provider = new SefazCeProvider(mockHttp, _parser, NullLogger<SefazCeProvider>.Instance);

        // Act
        var result = provider.CanHandle(url);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void SefazCeHtmlParser_ShouldParseSampleHtmlSuccessfully()
    {
        // Arrange
        var html = @"
            <!DOCTYPE html>
            <html>
            <body>
                <div id='txtNomeFantasia'>Supermercado Mezael</div>
                <div id='txtCnpj'>CNPJ: 06.509.999/0001-00</div>
                <span id='chave'>23230706509999000100650010000012341234567890</span>
                <span id='txtDataEmissao'>Emissão: 20/07/2026 14:30:00</span>
                <div id='txtTotal'>35,35</div>
                <div id='txtFormaPagamento'>Cartão de Crédito</div>
                <span id='txtCpf'>CPF do Consumidor: 123.456.789-00</span>
                <span id='txtNomeConsumer'>João da Silva</span>
                <table id='tabResult'>
                    <tr id='Item1'>
                        <td><span class='txtTit'>RAÇÃO PEDIGREE CARNE</span></td>
                        <td><span class='RCod'>Código: (789123456)</span></td>
                        <td><span class='Rqty'>Qtde.: 1,965</span></td>
                        <td><span class='RUN'>UN: KG</span></td>
                        <td><span class='RvlUnit'>Vl. Unit.: 17,99</span></td>
                        <td><span class='valor'>35,35</span></td>
                    </tr>
                </table>
            </body>
            </html>";

        // Act
        var invoice = _parser.Parse(html);

        // Assert
        Assert.NotNull(invoice);
        Assert.Equal("Supermercado Mezael", invoice.StoreName);
        Assert.Equal("06.509.999/0001-00", invoice.Cnpj);
        Assert.Equal("23230706509999000100650010000012341234567890", invoice.AccessKey);
        Assert.Equal(35.35m, invoice.Total);
        Assert.Equal("12345678900", invoice.Consumer.Cpf);
        Assert.Equal("João da Silva", invoice.Consumer.Name);
        
        Assert.Single(invoice.Items);
        var item = invoice.Items.First();
        Assert.Equal("RAÇÃO PEDIGREE CARNE", item.Description);
        Assert.Equal("789123456", item.Code);
        Assert.Equal(1.965m, item.Quantity);
        Assert.Equal("KG", item.Unit);
        Assert.Equal(17.99m, item.UnitPrice);
        Assert.Equal(35.35m, item.Total);
    }

    [Fact]
    public async Task InvoiceScanService_WithInvalidUrl_ReturnsInvalidQrCodeError()
    {
        // Arrange
        var validator = new ScanInvoiceRequestValidator();
        var service = new InvoiceScanService(
            Enumerable.Empty<IInvoiceProvider>(),
            validator,
            NullLogger<InvoiceScanService>.Instance
        );

        // Act
        var result = await service.ScanAsync("not-a-valid-url", CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.InvalidQrCode, result.Error.Type);
    }

    [Fact]
    public async Task InvoiceScanService_WithUnsupportedState_ReturnsUnsupportedStateError()
    {
        // Arrange
        var mockHttp = new MockHttpClient("");
        var provider = new SefazCeProvider(mockHttp, _parser, NullLogger<SefazCeProvider>.Instance);
        var validator = new ScanInvoiceRequestValidator();
        var service = new InvoiceScanService(
            new[] { provider },
            validator,
            NullLogger<InvoiceScanService>.Instance
        );

        // Act (URL do estado de São Paulo)
        var result = await service.ScanAsync("https://nfce.sefaz.sp.gov.br/consultanfce?p=352307...", CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.UnsupportedState, result.Error.Type);
    }

    private class MockHttpClient : IInvoiceHttpClient
    {
        private readonly string _htmlToReturn;

        public MockHttpClient(string htmlToReturn)
        {
            _htmlToReturn = htmlToReturn;
        }

        public Task<string> DownloadHtmlAsync(string url, CancellationToken cancellationToken)
        {
            return Task.FromResult(_htmlToReturn);
        }
    }
}
