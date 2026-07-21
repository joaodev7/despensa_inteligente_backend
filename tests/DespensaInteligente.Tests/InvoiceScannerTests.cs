using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using FluentValidation;
using DespensaInteligente.Application.InvoiceScanner.Common;
using DespensaInteligente.Application.InvoiceScanner.DTOs;
using DespensaInteligente.Application.InvoiceScanner.Interfaces;
using DespensaInteligente.Application.InvoiceScanner.Services;
using DespensaInteligente.Application.InvoiceScanner.Validators;
using DespensaInteligente.Infrastructure.InvoiceScanner.Http;
using DespensaInteligente.Infrastructure.InvoiceScanner.Models;
using DespensaInteligente.Infrastructure.InvoiceScanner.Parsers;
using DespensaInteligente.Infrastructure.InvoiceScanner.Providers;
using Xunit;

namespace DespensaInteligente.Tests;

public class InvoiceScannerTests
{
    private readonly SefazCeHtmlParser _parser;
    private readonly SefazCeQrCodeParser _qrCodeParser;

    public InvoiceScannerTests()
    {
        _parser = new SefazCeHtmlParser(NullLogger<SefazCeHtmlParser>.Instance);
        _qrCodeParser = new SefazCeQrCodeParser(NullLogger<SefazCeQrCodeParser>.Instance);
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
        var mockApiClient = new MockSefazCeApiClient("");
        var provider = new SefazCeProvider(_qrCodeParser, mockApiClient, _parser, NullLogger<SefazCeProvider>.Instance);

        // Act
        var result = provider.CanHandle(url);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void SefazCeQrCodeParser_ShouldParsePipeSeparatedUrlCorrectly()
    {
        // Arrange: Modelo 65 v2 oficial SEFAZ-CE (Chave | Versao | Ambiente | CSC | Hash)
        var qrCodeUrl = "https://nfce.sefaz.ce.gov.br/pages/ShowNFCe.html?p=23230706509999000100650010000012341234567890|2|1|2|A1B2C3D4E5F67890";

        // Act
        var payload = _qrCodeParser.Parse(qrCodeUrl);

        // Assert
        Assert.NotNull(payload);
        Assert.Equal("23230706509999000100650010000012341234567890", payload.ChaveAcesso);
        Assert.Equal(2, payload.VersaoQrCode);
        Assert.Equal(1, payload.TipoAmbiente);
        Assert.Equal("2", payload.IdentificadorCSC);
        Assert.Equal("A1B2C3D4E5F67890", payload.CodigoHash);
    }

    [Fact]
    public void SefazCeHtmlParser_ShouldParseRealSefazCeHtmlSuccessfully()
    {
        // Arrange - HTML real contido no atributo 'xml' do JSON da SEFAZ-CE
        var html = @"
            <!DOCTYPE html>
            <html>
            <body>
                <div id='txtNomeFantasia'>SUPERMERCADO MEZAEL</div>
                <div id='txtCnpj'>CNPJ: 06.509.999/0001-00</div>
                <span id='chave'>23230706509999000100650010000012341234567890</span>
                <span id='txtDataEmissao'>Emissão: 20/07/2026 14:30:00</span>
                <div id='txtTotal'>35,35</div>
                <div id='txtFormaPagamento'>Cartão de Crédito</div>
                <span id='txtCpf'>123.456.789-00</span>
                <span id='txtNomeConsumer'>JOAO DA SILVA</span>
                <table id='tabResult'>
                    <tbody>
                        <tr id='Item + 1'>
                            <td>
                                <span class='txtTit'>
                                    RACAO PEDIGREE CARNE F
                                </span>
                                <span class='RCod'>
                                    (Código:401534)
                                </span>
                                <br>
                                <span class='Rqtd'>
                                    Qtde.:1,965
                                </span>
                                <span class='RUN'>
                                    UN:KG
                                </span>
                                <span class='RvlUnit'>
                                    Vl. Unit.:17,99
                                </span>
                            </td>
                            <td class='txtTit noWrap' align='right'>
                                35,35
                            </td>
                        </tr>
                    </tbody>
                </table>
            </body>
            </html>";

        // Act
        var invoice = _parser.Parse(html);

        // Assert
        Assert.NotNull(invoice);
        Assert.Equal("SUPERMERCADO MEZAEL", invoice.StoreName);
        Assert.Equal("06.509.999/0001-00", invoice.Cnpj);
        Assert.Equal("23230706509999000100650010000012341234567890", invoice.AccessKey);
        Assert.Equal(35.35m, invoice.Total);
        Assert.Equal("12345678900", invoice.Consumer.Cpf);
        Assert.Equal("JOAO DA SILVA", invoice.Consumer.Name);
        
        Assert.Single(invoice.Items);
        var item = invoice.Items.First();
        Assert.Equal("RACAO PEDIGREE CARNE F", item.Description);
        Assert.Equal("401534", item.Code);
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
        var mockApiClient = new MockSefazCeApiClient("");
        var provider = new SefazCeProvider(_qrCodeParser, mockApiClient, _parser, NullLogger<SefazCeProvider>.Instance);
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

    private class MockSefazCeApiClient : ISefazCeApiClient
    {
        private readonly string _xmlToReturn;

        public MockSefazCeApiClient(string xmlToReturn)
        {
            _xmlToReturn = xmlToReturn;
        }

        public Task<SefazCeApiResponse> FetchInvoiceHtmlAsync(SefazCeQrCodePayload payload, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SefazCeApiResponse
            {
                Xml = _xmlToReturn,
                Erro = string.Empty
            });
        }
    }
}
