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
        NotasPrefeituraSPClient client = new (digital, cnpj);

        RetornoConsultaCNPJ result = client.ConsultaCNPJ(60316817000103);
        response = result.Cabecalho.Sucesso ? 
            new { data = result.Detalhe[0]}:
            new { data = "result.Cabecalho.Sucesso = false" };
        // string result = client.ConsultaCNPJ_Com("60316817000103");
        return new { result };
    }catch(Exception ex){
        response = new { data = $"ERRO: {ex.Message}" };
    }
    return response;
})
.WithName("Consulta CNPJ")
.WithOpenApi();

app.MapGet("teste-envio-lote-rps/{cnpj}/{digital}", async (string cnpj, string digital) =>
{
    NotasPrefeituraSPClient client = new (digital, cnpj);
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
            },
            MatriculaObra = 202500006685,
            NumeroEncapsulamento = 343788 
        },
    ];

    RetornoEnvioLoteRPS result = await client.TesteEnvioLoteRPSV2(listRps);
    return new
    {
        chaves = result.ChaveNFeRPS,
        data = result
    };
})
.WithName("Teste Envio Lote RPS")
.WithOpenApi();

app.Run();