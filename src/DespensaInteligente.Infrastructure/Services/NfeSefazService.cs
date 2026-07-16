using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DespensaInteligente.Application.Common.Interfaces;

namespace DespensaInteligente.Infrastructure.Services
{
    public class NfeSefazService : INfeSefazService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NfeSefazService> _logger;
        private readonly string _tipoAmbiente; // "1" for Production, "2" for Homologation

        public NfeSefazService(
            HttpClient httpClient,
            ILogger<NfeSefazService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _tipoAmbiente = configuration["Sefaz:TipoAmbiente"] ?? "1";

            // Configure browser User-Agent
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public async Task<string> ConsultarNfeAsync(string? url, string? chave)
        {
            if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(chave))
            {
                throw new ArgumentException("É necessário informar a URL do QR Code ou a Chave de Acesso da NFC-e.");
            }

            string accessKey = "";
            if (!string.IsNullOrWhiteSpace(chave))
            {
                accessKey = Regex.Replace(chave, @"\D", "");
            }
            else if (!string.IsNullOrWhiteSpace(url))
            {
                accessKey = ExtractChaveFromUrl(url);
            }

            if (accessKey.Length != 44)
            {
                throw new ArgumentException($"Chave de acesso inválida. Deve conter 44 dígitos numéricos. Chave extraída: '{accessKey}'");
            }

            string ufCode = accessKey.Substring(0, 2);
            _logger.LogInformation("Consultando NFC-e para a UF {UfCode} com chave {Chave}", ufCode, accessKey);

            try
            {
                // Ceará (UF 23)
                if (ufCode == "23")
                {
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        throw new ArgumentException("A SEFAZ do Ceará (UF 23) exige a URL completa do QR Code para consulta.");
                    }
                    return await ConsultarSefazCearaAsync(url);
                }

                // Outras UFs - SP, RJ, MG, RS, PR, SC, BA, GO, DF
                return await ConsultarOutrasUfsAsync(ufCode, accessKey, url);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro de rede ao consultar SEFAZ para UF {UfCode}", ufCode);
                throw new Exception($"Falha ao consultar a SEFAZ do estado {GetNomeUf(ufCode)}. O servidor pode estar fora do ar ou bloqueando a requisição. Detalhe: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout ao consultar SEFAZ para UF {UfCode}", ufCode);
                throw new Exception($"Tempo limite esgotado ao consultar a SEFAZ do estado {GetNomeUf(ufCode)} (Timeout). Tente o upload do arquivo.", ex);
            }
        }

        private string ExtractChaveFromUrl(string url)
        {
            // Usually, NFC-e URLs have a parameter chNFe, p, or keys in paths
            var match = Regex.Match(url, @"[0-9]{44}");
            if (match.Success)
            {
                return match.Value;
            }

            var uri = new Uri(url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            string[] possibleParams = { "chNFe", "p", "ch", "chave" };
            foreach (var param in possibleParams)
            {
                var val = query[param];
                if (val != null)
                {
                    var cleanVal = Regex.Replace(val, @"\D", "");
                    if (cleanVal.Length == 44) return cleanVal;
                }
            }

            throw new ArgumentException("Não foi possível extrair a chave de acesso de 44 dígitos da URL informada.");
        }

        private async Task<string> ConsultarSefazCearaAsync(string url)
        {
            // Ceará has endpoints for v2 and v3
            // v2: http://nfce.sefaz.ce.gov.br/nfce/api/notasFiscal/qrcodev2/
            // v3: http://nfce.sefaz.ce.gov.br/nfce/api/notasFiscal/qrcodev3/
            // Host is nfceh.sefaz.ce.gov.br in homologation (tipo_ambiente = 2)

            string host = _tipoAmbiente == "2" ? "nfceh.sefaz.ce.gov.br" : "nfce.sefaz.ce.gov.br";
            string version = url.Contains("qrcodev3") || url.Contains("v=3") ? "qrcodev3" : "qrcodev2";

            string endpoint = $"http://{host}/nfce/api/notasFiscal/{version}/";

            _logger.LogInformation("Efetuando POST para SEFAZ CE em {Endpoint}", endpoint);

            var payload = new { qrcode = url };
            var response = await _httpClient.PostAsJsonAsync(endpoint, payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erro retornado pela SEFAZ CE: Código {response.StatusCode} - {errorText}");
            }

            var result = await response.Content.ReadFromJsonAsync<SefazCeResponse>();
            if (result == null || string.IsNullOrWhiteSpace(result.Xml))
            {
                throw new Exception("Resposta inválida da SEFAZ CE. O XML não foi retornado.");
            }

            return result.Xml;
        }

        private async Task<string> ConsultarOutrasUfsAsync(string ufCode, string accessKey, string? url)
        {
            // Mapeando portais das UFs
            string portalUrl = ufCode switch
            {
                "35" => $"https://www.nfce.fazenda.sp.gov.br/NFCePortal/Paginas/ConsultaPublica.aspx?ch={accessKey}", // SP
                "33" => $"https://www.fazenda.rj.gov.br/nfce/consulta?ch={accessKey}", // RJ
                "31" => $"https://portalsped.fazenda.mg.gov.br/portalsped/sistema/consulta.xhtml?ch={accessKey}", // MG
                "43" => $"https://www.sefaz.rs.gov.br/NFCE/NFCE-COM.aspx?ch={accessKey}", // RS
                "41" => $"http://www.fazenda.pr.gov.br/nfce/consulta?ch={accessKey}", // PR
                "42" => $"https://sat.sef.sc.gov.br/nfce/consulta?ch={accessKey}", // SC
                "29" => $"https://sistemas.sefaz.ba.gov.br/nfce/consulta?ch={accessKey}", // BA
                "52" => $"https://gissonline.sefaz.go.gov.br/nfce/consulta?ch={accessKey}", // GO
                "53" => $"https://dec.fazenda.df.gov.br/ConsultarNFCe.aspx?ch={accessKey}", // DF
                _ => url ?? $"https://www.portalfiscal.inf.br/nfe/consulta?ch={accessKey}"
            };

            _logger.LogInformation("Efetuando GET para portal da UF {UfCode} em {PortalUrl}", ufCode, portalUrl);

            // Attempt to perform GET request. If it fails or is blocked by cloudflare/recaptcha, 
            // we will return a descriptive error that redirects to upload or shows simulated extraction
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                var response = await _httpClient.GetAsync(portalUrl, cts.Token);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Consulta direta à UF {UfCode} falhou. Simulando fallback ou avisando o usuário.", ufCode);
                // Return a descriptive mock/scraped representation or throw exception to fall back to LLM direct parsing 
                // if we have HTML, or return a simulated response if we are in a testing/dev sandbox environment.
                
                // Let's check if the URL contains some mock keyword for testing, or return a simulated HTML.
                if (url != null && url.Contains("mock=true"))
                {
                    return GetMockHtml(accessKey);
                }

                throw; // propagate to let the system know direct query failed, prompting fallback to file upload.
            }
        }

        private string GetMockHtml(string key)
        {
            return $@"
            <html>
                <body>
                    <div id='chave'>{key}</div>
                    <div class='txtCNPJ'>12.345.678/0001-90</div>
                    <div class='txtMercado'>SUPERMERCADO PARCEIRO LTDA</div>
                    <div class='dataCompra'>15/07/2026 14:30:00</div>
                    <div class='total'>125.50</div>
                    <table>
                        <tr class='item'>
                            <td class='nome'>ARROZ INTEGRAL 1KG</td>
                            <td class='qtd'>2.0</td>
                            <td class='un'>un</td>
                            <td class='preco_unit'>10.50</td>
                            <td class='preco_total'>21.00</td>
                        </tr>
                        <tr class='item'>
                            <td class='nome'>FEIJAO PRETO 1KG</td>
                            <td class='qtd'>3.0</td>
                            <td class='un'>pct</td>
                            <td class='preco_unit'>9.00</td>
                            <td class='preco_total'>27.00</td>
                        </tr>
                    </table>
                </body>
            </html>";
        }

        private string GetNomeUf(string code)
        {
            return code switch
            {
                "35" => "São Paulo (SP)",
                "33" => "Rio de Janeiro (RJ)",
                "31" => "Minas Gerais (MG)",
                "43" => "Rio Grande do Sul (RS)",
                "41" => "Paraná (PR)",
                "42" => "Santa Catarina (SC)",
                "29" => "Bahia (BA)",
                "52" => "Goiás (GO)",
                "53" => "Distrito Federal (DF)",
                "23" => "Ceará (CE)",
                _ => "Não Mapeada"
            };
        }

        private class SefazCeResponse
        {
            public string Xml { get; set; } = string.Empty;
        }
    }
}
