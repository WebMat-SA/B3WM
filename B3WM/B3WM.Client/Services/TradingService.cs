using System.Net.Http.Json;
using System.Text.Json;
using B3WM.Client.Model;

namespace B3WM.Client.Services;

public class TradingService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public TradingService(HttpClient http)
    {
        _http = http;
    }

    public async Task<OrderResult?> PlaceMarketOrder(MarketOrderRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/trade/order-market", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderResult>(JsonOptions);
    }

    public async Task<OrderResult?> ClosePosition(long positionTicket)
    {
        var request = new CloseOrderRequest { PositionTicket = positionTicket };
        var response = await _http.PostAsJsonAsync("/api/trade/order-close", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderResult>(JsonOptions);
    }

    public async Task<OrderResult?> ModifyPosition(long positionTicket, double? sl = null, double? tp = null)
    {
        var request = new ModifyOrderRequest
        {
            PositionTicket = positionTicket,
            Sl = sl,
            Tp = tp,
        };
        var response = await _http.PostAsJsonAsync("/api/trade/order-modify", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderResult>(JsonOptions);
    }

    public async Task<AccountInfo?> GetAccountInfo()
    {
        return await _http.GetFromJsonAsync<AccountInfo>("/api/trade/account", JsonOptions);
    }

    public async Task<List<PositionInfo>> GetPositions(string? symbol = null)
    {
        var url = symbol is not null ? $"/api/trade/positions/{symbol}" : "/api/trade/positions";
        var result = await _http.GetFromJsonAsync<List<PositionInfo>>(url, JsonOptions);
        return result ?? [];
    }

    public async Task<SymbolInfo?> GetSymbolInfo(string symbol)
    {
        return await _http.GetFromJsonAsync<SymbolInfo>($"/api/trade/symbol/{symbol}", JsonOptions);
    }
}
