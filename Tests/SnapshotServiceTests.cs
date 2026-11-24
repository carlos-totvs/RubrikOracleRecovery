using System.Net;
using System.Text;
using System.Text.Json;
using RubrikOracleRecovery.Configuration; // Namespace onde está RscCredentials
using RubrikOracleRecovery.Model;         // Namespace onde está OracleRecoveryPoint
using RubrikOracleRecovery.Services;      // Namespace onde está SnapshotService

namespace Tests;

public class SnapshotServiceTests
{
    private RscCredentials CriarCredenciaisTeste() => new()
    {
        ClientId = "client-id-teste",
        ClientSecret = "secret-teste",
        Name = "sa-rubrik-teste",
        AccessTokenUri = "https://rubrik.teste.local/api/client_token",
        GraphqlUrl = "https://rubrik.teste.local/api/graphql"
    };

    [Fact]
    public async Task GetRecoveryPointsAsync_DeveRetornarPontosDeRecuperacao_QuandoHostEncontrado()
    {
        // Arrange (Preparação)
        var dataEsperada = new DateTime(2026, 11, 19, 14, 30, 0, DateTimeKind.Utc);
        var hostConsultado = "srv-oracle-01";

        var handlerFalso = new FakeHttpMessageHandler(async (request, cancellationToken) =>
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);

            if (request.RequestUri!.ToString().Contains("/api/client_token"))
            {
                return CriarRespostaJson(new { access_token = "token-falso-123" });
            }

            if (body.Contains("OracleHostObjectSearch"))
            {
                return CriarRespostaJson(new
                {
                    data = new
                    {
                        globalSearchResults = new
                        {
                            nodes = new[]
                            {
                                new { id = "OracleDatabase:::1111-2222", name = "ORCL", objectType = "OracleDatabase" }
                            }
                        }
                    }
                });
            }

            if (body.Contains("DataRecuperaveisOracle"))
            {
                return CriarRespostaJson(new
                {
                    data = new
                    {
                        oracleRecoverableRanges = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    beginTime = "2026-11-18T14:30:00Z",
                                    endTime = dataEsperada.ToString("O"),
                                    dbSnapshotSummaries = new[]
                                    {
                                        new
                                        {
                                            databaseName = "ORCL",
                                            hostOrRacName = hostConsultado,
                                            tablespaces = new[] { "SYSTEM", "USERS" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        var service = new SnapshotService(CriarCredenciaisTeste(), new HttpClient(handlerFalso));

        var resultado = await service.GetRecoveryPointsAsync(new List<string> { hostConsultado });

        Assert.Single(resultado);
        Assert.Equal(hostConsultado, resultado[0].HostOrRacName);
        Assert.Equal(dataEsperada.ToUniversalTime(), resultado[0].LatestEndTime.ToUniversalTime());
        Assert.Equal(3, handlerFalso.QuantidadeChamadas);
    }

    [Fact]
    public async Task GetRecoveryPointsAsync_DeveIgnorarHost_QuandoNaoEncontradoNaRubrik()
    {
        // Arrange
        var handlerFalso = new FakeHttpMessageHandler(async (request, cancellationToken) =>
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);

            if (request.RequestUri!.ToString().Contains("/api/client_token"))
                return CriarRespostaJson(new { access_token = "token-falso-123" });

            if (body.Contains("OracleHostObjectSearch"))
            {
                return CriarRespostaJson(new
                {
                    data = new
                    {
                        globalSearchResults = new
                        {
                            nodes = Array.Empty<object>()
                        }
                    }
                });
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        var service = new SnapshotService(CriarCredenciaisTeste(), new HttpClient(handlerFalso));

        var resultado = await service.GetRecoveryPointsAsync(new List<string> { "host-inexistente" });

        Assert.Empty(resultado);
        Assert.Equal(2, handlerFalso.QuantidadeChamadas);
    }

    [Fact]
    public async Task GetRecoveryPointsAsync_DeveContinuarProcessandoProximoHost_QuandoOcorrerErroEmUmDeles()
    {
        var handlerFalso = new FakeHttpMessageHandler(async (request, cancellationToken) =>
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);

            if (request.RequestUri!.ToString().Contains("/api/client_token"))
                return CriarRespostaJson(new { access_token = "token-falso-123" });

            if (body.Contains("host-com-erro"))
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);

            if (body.Contains("OracleHostObjectSearch") && body.Contains("host-valido"))
            {
                return CriarRespostaJson(new
                {
                    data = new
                    {
                        globalSearchResults = new
                        {
                            nodes = new[] { new { id = "OracleDatabase:::9999", name = "PROD", objectType = "OracleDatabase" } }
                        }
                    }
                });
            }

            if (body.Contains("DataRecuperaveisOracle"))
            {
                return CriarRespostaJson(new
                {
                    data = new
                    {
                        oracleRecoverableRanges = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    beginTime = "2026-11-19T10:00:00Z",
                                    endTime = "2026-11-19T12:00:00Z",
                                    dbSnapshotSummaries = new[]
                                    {
                                        new { databaseName = "PROD", hostOrRacName = "host-valido", tablespaces = new[] { "USERS" } }
                                    }
                                }
                            }
                        }
                    }
                });
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        var service = new SnapshotService(CriarCredenciaisTeste(), new HttpClient(handlerFalso));

        var resultado = await service.GetRecoveryPointsAsync(new List<string> { "host-com-erro", "host-valido" });

        Assert.Single(resultado);
        Assert.Equal("host-valido", resultado[0].HostOrRacName);
    }

    private static HttpResponseMessage CriarRespostaJson(object payload)
    {
        return new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }
}

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
    public int QuantidadeChamadas { get; private set; }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        QuantidadeChamadas++;
        return _responder(request, cancellationToken);
    }
}