using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NotasPrefeituraSP;
using NotasPrefeituraSP.Models.Sp.Sync.Nfes;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("consulta-cnpj/{cnpj}/{digital}", (string cnpj, string digital) =>
{
    dynamic response = new { };
    try{
        // X509Certificate2 certificadoX509 = GetCredenciais(digital);
        // NotasPrefeituraSPClient client = new (digital, cnpj);
        NotasPrefeituraSPClient client = new();
        client.Init(digital, cnpj);

        // RetornoConsultaCNPJ result = client.ConsultaCNPJ("60316817000103");
        // response = result.Cabecalho.Sucesso ? 
        //     new { data = result.Detalhe[0]}:
        //     new { data = "result.Cabecalho.Sucesso = false" };
        string result = client.ConsultaCNPJ_Com("60316817000103");
        return new { result };
    }catch(Exception ex){
        response = new { data = $"ERRO: {ex.Message}" };
    }
    return response;
})
.WithName("Consulta CNPJ")
.WithOpenApi();

app.MapGet("teste-envio-lote-rps/{cnpj}/{digital}", (string cnpj, string digital) =>
{
    // NotasPrefeituraSPClient client = new (digital, cnpj);
    NotasPrefeituraSPClient client = new();
    client.Init(digital, cnpj);
    tpRPS[] listRps = [
        new (){
            RazaoSocialTomador = "SOLIDA ENGENHARIA LTDA",
            EnderecoTomador = new(){
                TipoLogradouro = "ROD",
                Logradouro = "AMARAL PEIXOTO",
                NumeroEndereco = "0",
                ComplementoEndereco = "KM 85",
                Bairro = "VILA CAPRI",
                Cidade = 3300209, // Código IBGE
                UF = "RJ",
                CEP = 28970000,
            },
            EmailTomador = "solida@solidaengenharia.com.br",
            Discriminacao = "Essa é a descrição dos serviços", // 404 - DescriçãoRPS ??? DescricaoServicoComplemento ???
            ChaveRPS = new(){
                InscricaoPrestador = 39901599, // Validar dps
                SerieRPS = "20",
                NumeroRPS = 5433,
            },
            TipoRPS = 0, // !!
            DataEmissao = DateTime.Parse("2025-12-01"), // DataFaturamento ??
            StatusRPS = 0, // !!!,
            TributacaoRPS = "T", // !!!, 
            ValorServicos = 2354.97M, // ValorTotal,
            ValorDeducoes = 0, // ValorDedPref
            ValorPIS = 0, // PercentualPISPASEP !!!
            ValorCOFINS = 0, // PercentualCOFINS !!!
            ValorINSS = 0, // DescricaoINSS !!!
            ValorIR = 0, // PercentualIR !!!
            ValorCSLL = 0, // PercentualCSLL !!!
            CodigoServicoS = "01023", // CodigoServicoRPS !!!
            AliquotaServicos = 0, // !!! ??
            ISSRetido = false,
            CPFCNPJTomador = new(){
                Item = "72045925000160"
            }
        },
    ];

    RetornoEnvioLoteRPS result = client.TesteEnvioLoteRPS(listRps);
    return new
    {
        chaves = result.ChaveNFeRPS,
        data = result
    };
})
.WithName("Teste Envio Lote RPS")
.WithOpenApi();


app.MapGet("teste-envio-lote-rps-nolib/{cnpj}/{digital}", async (string cnpj, string digital) => 
{
    X509Certificate2 certificadoX509 = GetCredenciais(digital);
    string stringXml = GetXml();
    string xmlAssinado = AssinarXml(stringXml, certificadoX509);
    string requestXml = EnvelopXml(xmlAssinado);

    string result = "";
    HttpClientHandler handler = new();
    handler.ClientCertificates.Add(certificadoX509);
    using (HttpClient client = new(handler))
    {
        StringContent content = new(requestXml, Encoding.UTF8, "text/xml");

        content.Headers.Clear();
        content.Headers.Add("Content-Type", "text/xml; charset=utf-8");
        content.Headers.Add("SOAPAction", "\"http://www.prefeitura.sp.gov.br/nfe/TesteEnvioLoteRPS\"");

        HttpResponseMessage response = await client.PostAsync("https://nfe.prefeitura.sp.gov.br/ws/lotenfe.asmx", content);

        result = await response.Content.ReadAsStringAsync();
    }

    return new
    {
        result = new { result },
        requestXml = new { requestXml }
    };

})
.WithName("Teste Envio Lote RPS NoLib")
.WithOpenApi();

static X509Certificate2 GetCredenciais(string digital)
{
    using (X509Store store = new(StoreName.My, StoreLocation.CurrentUser))
    {
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        X509Certificate2Collection certificados = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            digital,
            validOnly: false // true = apenas válidos, false = inclui expirados
        );

        // certificados[0].SerialNumber;

        if (certificados.Count == 0)
            throw new Exception($"Nenhum certificado encontrado contendo '{digital}'.");

        return certificados[0];
    }
}

static string AssinarXml(string xml, X509Certificate2 certificado)
{
    XmlDocument doc = new ();
    doc.PreserveWhitespace = true;
    doc.LoadXml(xml);

    XmlNamespaceManager nsmgr = new (doc.NameTable);
    nsmgr.AddNamespace("nfe", "http://www.prefeitura.sp.gov.br/nfe");

    XmlNode rps = doc.SelectSingleNode("//*[local-name()='RPS']", nsmgr)!;

    // Extrair campos obrigatórios
    string inscricaoPrestador = rps.SelectSingleNode("//*[local-name()='InscricaoPrestador']", nsmgr)?.InnerText ?? "";
    string serieRps = rps.SelectSingleNode("//*[local-name()='SerieRPS']", nsmgr)?.InnerText ?? "";
    string numeroRps = rps.SelectSingleNode("//*[local-name()='NumeroRPS']", nsmgr)?.InnerText ?? "";
    string dataEmissao = rps.SelectSingleNode("//*[local-name()='DataEmissao']", nsmgr)?.InnerText ?? "";
    string tipoRps = rps.SelectSingleNode("//*[local-name()='TipoRPS']", nsmgr)?.InnerText ?? "";
    string statusRps = rps.SelectSingleNode("//*[local-name()='StatusRPS']", nsmgr)?.InnerText ?? "";
    string codigoServico = rps.SelectSingleNode("//*[local-name()='CodigoServico']", nsmgr)?.InnerText ?? "";
    string valorServicos = rps.SelectSingleNode("//*[local-name()='ValorServicos']", nsmgr)?.InnerText ?? "0";
    string valorDeducoes = rps.SelectSingleNode("//*[local-name()='ValorDeducoes']", nsmgr)?.InnerText ?? "0";
    string issRetido = rps.SelectSingleNode("//*[local-name()='ISSRetido']", nsmgr)?.InnerText ?? "false";
    string cnpjTomador = rps.SelectSingleNode("//*[local-name()='CNPJ']", nsmgr)?.InnerText ?? "";

    // Converter ISSRetido para S/N
    issRetido = (issRetido.ToLower().Equals("true") || issRetido == "1") ? "S" : "N";

    // Formatar data
    string dataFormatada = DateTime.Parse(dataEmissao).ToString("yyyyMMdd");

    // Montar string base
    string assinaturaTexto = inscricaoPrestador
        + serieRps
        + numeroRps
        + dataFormatada
        + tipoRps
        + statusRps
        + codigoServico
        + valorServicos.Replace(",", "").Replace(".", "")
        + valorDeducoes.Replace(",", "").Replace(".", "")
        + issRetido
        + cnpjTomador;

    // Calcular hash SHA1 em base64
    using (SHA1 sha1 = SHA1.Create())
    {
        byte[] hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(assinaturaTexto));
        string hashBase64 = Convert.ToBase64String(hashBytes);

        // Inserir tag <Assinatura> logo após <ChaveRPS>
        XmlElement assinaturaEl = doc.CreateElement("Assinatura");
        assinaturaEl.InnerText = hashBase64;

        XmlNode chaveRps = rps.SelectSingleNode("//*[local-name()='ChaveRPS']", nsmgr)!;
        if (chaveRps != null && chaveRps.ParentNode != null)
        {
            rps.InsertAfter(assinaturaEl, chaveRps);
        }
    }

    return doc.OuterXml;
}

static string GetXml()
{
    return @"
<PedidoEnvioLoteRPS
	xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
	xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
	xmlns=""http://www.prefeitura.sp.gov.br/nfe"">
	<Cabecalho Versao=""1"" xmlns="""">
		<CPFCNPJRemetente>
			<CNPJ>29067113000196</CNPJ>
		</CPFCNPJRemetente>
		<dtInicio>2025-11-12</dtInicio>
		<dtFim>2025-11-12</dtFim>
		<QtdRPS>1</QtdRPS>
		<ValorTotalServicos>2354.97</ValorTotalServicos>
	</Cabecalho>
	<RPS xmlns="""">
		<ChaveRPS>
			<InscricaoPrestador>62900</InscricaoPrestador>
			<SerieRPS>20</SerieRPS>
			<NumeroRPS>5433</NumeroRPS>
		</ChaveRPS>
		<TipoRPS>RPS</TipoRPS>
		<DataEmissao>2025-11-12</DataEmissao>
		<StatusRPS>N</StatusRPS>
		<TributacaoRPS>T</TributacaoRPS>
		<ValorServicos>2354.97</ValorServicos>
		<ValorDeducoes>0</ValorDeducoes>
		<ValorPIS>0</ValorPIS>
		<ValorCOFINS>0</ValorCOFINS>
		<ValorINSS>0</ValorINSS>
		<ValorIR>0</ValorIR>
		<ValorCSLL>0</ValorCSLL>
		<CodigoServico>11023</CodigoServico>
		<AliquotaServicos>1</AliquotaServicos>
		<ISSRetido>false</ISSRetido>
		<CPFCNPJTomador>
			<CNPJ>72045925000160</CNPJ>
		</CPFCNPJTomador>
		<InscricaoEstadualTomador>0</InscricaoEstadualTomador>
		<RazaoSocialTomador>SOLIDA ENGENHARIA LTDA</RazaoSocialTomador>
		<EnderecoTomador>
			<TipoLogradouro>ROD</TipoLogradouro>
			<Logradouro>AMARAL PEIXOTO</Logradouro>
			<NumeroEndereco>0</NumeroEndereco>
			<ComplementoEndereco>KM 85</ComplementoEndereco>
			<Bairro>VILA CAPRI</Bairro>
			<Cidade>3300209</Cidade>
			<UF>RJ</UF>
			<CEP>28970000</CEP>
		</EnderecoTomador>
		<EmailTomador>solida@solidaengenharia.com.br</EmailTomador>
		<InscricaoMunicipalIntermediario>0</InscricaoMunicipalIntermediario>
		<ISSRetidoIntermediario>false</ISSRetidoIntermediario>
		<Discriminacao>Essa é a descrição dos serviços</Discriminacao>
		<ValorCargaTributaria>0</ValorCargaTributaria>
		<PercentualCargaTributaria>0</PercentualCargaTributaria>
		<FonteCargaTributaria>teste</FonteCargaTributaria>
		<CodigoCEI>0</CodigoCEI>
		<MatriculaObra>0</MatriculaObra>
		<MunicipioPrestacao>3300209</MunicipioPrestacao>
		<NumeroEncapsulamento>0</NumeroEncapsulamento>
		<ValorTotalRecebido>0</ValorTotalRecebido>
	</RPS>
</PedidoEnvioLoteRPS>";
}

static string EnvelopXml(string xml)
{
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <TesteEnvioLoteRPS xmlns=""http://www.prefeitura.sp.gov.br/nfe"">
      <VersaoSchema>1</VersaoSchema>
      <MensagemXML><![CDATA[{xml}]]></MensagemXML>
    </TesteEnvioLoteRPS>
  </soap:Body>
</soap:Envelope>";

}
app.Run();


// select new
//                                  {
//                                      RazaoSocialTomador = f.RazaoSocial,
//                                      EnderecoTomador = new tpEndereco()
//                                      {
//                                          TipoLogradouro = l.Descricao.Substring(0,3),
//                                          Logradouro = f.Endereco,
//                                          NumeroEndereco = f.NumeroDoEndereco,
//                                          ComplementoEndereco = f.Complemento,
//                                          Bairro = f.Bairro,
//                                          Cidade = m.CodigoRais,
//                                          UF = m.Sigla,
//                                          CEP = ConvertToInt(f.CEP),
//                                      },
//                                      EmailTomador = cli.Email,
//                                      Discriminacao = dto.Discriminacao,
//                                      ChaveRPS = new tpChaveRPS()
//                                      {
//                                          InscricaoPrestador = dto.InscricaoPrestador,
//                                          SerieRPS = f.SerieRPS,
//                                          NumeroRPS = f.NumeroRPS,
//                                      },
//                                      TipoRPS = dto.TipoRPS,
//                                      DataEmissao = f.DataFaturamento,
//                                      StatusRPS = dto.StatusRPS,
//                                      TributacaoRPS = dto.TributacaoRPS, 
//                                      ValorServicos = f.ValorTotal, 
//                                      ValorDeducoes = f.ValorDedPref ?? 0,
//                                      ValorPIS = f.PercentualPISPASEP ?? 0,
//                                      ValorCOFINS = f.PercentualCOFINS ?? 0,
//                                      ValorINSS = dto.AliquotaServicos, // DescricaoINSS !!!
//                                      ValorIR = f.PercentualIR ?? 0, 
//                                      ValorCSLL = f.PercentualCSLL ?? 0, 
//                                      CodigoServicoS = f.CodigoServicoRPS ?? "01023",
//                                      AliquotaServicos = dto.AliquotaServicos,
//                                      ISSRetido = dto.ISSRetido,
//                                      CPFCNPJTomador = new tpCPFCNPJ()
//                                      {
//                                          Item = cli.CNPJCPF
//                                      }
//                                  }).ToArray();