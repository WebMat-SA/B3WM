using System.Net;
using System.Text;
using System.Text.Json;
using B3WM.Services.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace B3WM.Tests;

/// <summary>
/// O corpo montado por <see cref="JevService.BuildRequest"/> é exatamente o
/// texto enviado no HTTP e o mesmo que a auditoria deve gravar em Payload.
/// </summary>
public class JevServiceTests
{
    [Fact]
    public async Task BuildRequest_Json_IsExactlyTheBodySent()
    {
        var handler = new CaptureHandler();
        var config = new ConfigurationManager
        {
            ["JevKey"] = "test-key",
            ["TypeSafe:Model"] = "jev-latest",
        };
        var service = new JevService(
            new Factory(handler), config, NullLogger<JevService>.Instance);
        var state = new Dictionary<string, object?>
        {
            ["symbol"] = "WINFUT",
            ["n"] = 1,
        };
        var questions = new Dictionary<string, object?>
        {
            ["candles_alta"] = "pergunta",
        };

        var request = service.BuildRequest(state, questions);
        var response = await service.AskAsync(request);

        Assert.Equal(request.Json, handler.Body);
        Assert.Equal(200, response.HttpStatus);
        Assert.Equal(JsonSerializer.Serialize(state).Length, request.StateChars);
        Assert.StartsWith("{\"state\":{", request.Json);
        Assert.Contains("\"model\":\"jev-latest\"", request.Json);
        Assert.Contains("\"questions\":{", request.Json);
        Assert.Contains("\"candles_alta\":\"pergunta\"", request.Json);
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"model":"jev-latest","usage":{"input_tokens":11,"output_tokens":22}}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }
}
