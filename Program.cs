using System.Security.Cryptography.X509Certificates;
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
        X509Certificate2 certificadoX509 = GetCredenciais(digital);
        NotasPrefeituraSPClient client = new (certificadoX509, cnpj);
        RetornoConsultaCNPJ result = client.ConsultaCNPJ(60316817000103);
        response = result.Cabecalho.Sucesso ? 
            new { data = result.Detalhe[0]}:
            new { data = "result.Cabecalho.Sucesso = false" };
    }catch(Exception ex){
        response = new { data = $"ERRO: {ex.Message}" };
    }
    return response;
})
.WithName("Consulta CNPJ")
.WithOpenApi();

app.MapGet("teste-envio-lote-rps/{cnpj}/{digital}", (string cnpj, string digital) =>
{
    X509Certificate2 certificadoX509 = GetCredenciais(digital);
    NotasPrefeituraSPClient client = new (certificadoX509, cnpj);
    tpRPS[] listRps = [
        new(){
            RazaoSocialTomador = "JESUS ABEAD TOLOSANA",
            EnderecoTomador = new(){
                TipoLogradouro = "Rua",
                Logradouro = "MONTE DA GAMELEIRA",
                NumeroEndereco = "11",
                ComplementoEndereco = "ESQUINA DA RUA TUCANO",
                Bairro = "JARDIM CALIFORNIA",
                Cidade = 3505708, // Código IBGE
                CidadeSpecified = true,
                UF = "SP",
                CEP = 06409050,
                CEPSpecified = true
            },
            EmailTomador = "", // !!!
            // CPFCNPJIntermediario = new(){} NÃO TEM
            // InscricaoMunicipalIntermediario =  NÃO TEM
            // ISSRetidoIntermediario =  NÃO TEM
            // EmailIntermediario = NÃO TEM
            Discriminacao = "", // !!!
            ValorCargaTributaria = 0, // !!! 
            PercentualCargaTributaria = 0, // !!! 
            FonteCargaTributaria = "", // !!!
            CodigoCEI = 0, // !!!
            MatriculaObra = 0, // !!!,
            MunicipioPrestacao = 3505708,
            NumeroEncapsulamento = 0,  // !!! ???
            InscricaoEstadualTomador = 0, // !!!
            InscricaoMunicipalTomador = 0, //  !!! null
            ChaveRPS = new(){
                InscricaoPrestador = 0, // ??
                SerieRPS = "18",
                NumeroRPS = 10808,
            },
            TipoRPS = 0,
            DataEmissao = DateTime.Parse("2018-08-11"), // DataFaturamento ??
            StatusRPS = 0, // !!!,
            TributacaoRPS = "false", // !!!, 
            ValorServicos = 24.50M, // ValorAposVencimento,
            ValorDeducoes = 0M, // AliquotaDedPref
            ValorPIS = 0, // PercentualPISPASEP !!!
            ValorTotalRecebido = 24.50M, // ValorAposVencimento !!!
            ValorCOFINS = 0, // PercentualCOFINS !!!
            ValorINSS = 0, // DescricaoINSS !!!
            ValorIR = 0, // PercentualIR !!!
            ValorCSLL = 0, // PercentualCSLL !!!
            CodigoServico = 0, // CodigoServicoRPS !!!
            AliquotaServicos = 0, // !!! ??
            ISSRetido = true,
            CPFCNPJTomador = new(){
                Item = "58577424804"
            }
        }
    ];
    RetornoEnvioLoteRPS result = client.TesteEnvioLoteRPS(listRps);
    return new
    {
        chaves = result.ChaveNFeRPS,
        data = result
    };
})
.WithName("Teste-Envio Lote RPS")
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

        if (certificados.Count == 0)
            throw new Exception($"Nenhum certificado encontrado contendo '{digital}'.");

        return certificados[0];
    }
}

app.Run();