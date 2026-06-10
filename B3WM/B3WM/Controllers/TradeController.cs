using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace B3WM.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TradeController : ControllerBase
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public TradeController(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("PythonService");
    }

    [HttpPost("order-market")]
    public async Task<IActionResult> PlaceMarketOrder([FromBody] JsonElement body)
    {
        return await ForwardPost("/api/order/market", body);
    }

    [HttpPost("order-close")]
    public async Task<IActionResult> ClosePosition([FromBody] JsonElement body)
    {
        return await ForwardPost("/api/order/close", body);
    }

    [HttpPost("order-modify")]
    public async Task<IActionResult> ModifyPosition([FromBody] JsonElement body)
    {
        return await ForwardPost("/api/order/modify", body);
    }

    [HttpGet("account")]
    public async Task<IActionResult> GetAccountInfo()
    {
        return await ForwardGet("/api/account");
    }

    [HttpGet("positions")]
    public async Task<IActionResult> GetPositions()
    {
        return await ForwardGet("/api/positions");
    }

    [HttpGet("positions/{symbol}")]
    public async Task<IActionResult> GetPositionsBySymbol(string symbol)
    {
        return await ForwardGet($"/api/positions/{symbol}");
    }

    [HttpGet("symbol/{symbol}")]
    public async Task<IActionResult> GetSymbolInfo(string symbol)
    {
        return await ForwardGet($"/api/symbol/{symbol}");
    }

    private async Task<IActionResult> ForwardPost(string path, JsonElement body)
    {
        var json = body.GetRawText();
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(path, content);
        var result = await response.Content.ReadAsStringAsync();
        return StatusCode((int)response.StatusCode, result);
    }

    private async Task<IActionResult> ForwardGet(string path)
    {
        var response = await _http.GetAsync(path);
        var result = await response.Content.ReadAsStringAsync();
        return StatusCode((int)response.StatusCode, result);
    }
}
