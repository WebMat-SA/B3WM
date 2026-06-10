using System.Text.Json.Serialization;

namespace B3WM.Client.Model;

public class MarketOrderRequest
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "";

    [JsonPropertyName("volume")]
    public double Volume { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = ""; // "buy" or "sell"

    [JsonPropertyName("sl")]
    public double? Sl { get; set; }

    [JsonPropertyName("tp")]
    public double? Tp { get; set; }

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";

    [JsonPropertyName("magic")]
    public int Magic { get; set; }

    [JsonPropertyName("deviation")]
    public int Deviation { get; set; } = 10;
}

public class OrderResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("retcode")]
    public int Retcode { get; set; }

    [JsonPropertyName("retcode_name")]
    public string RetcodeName { get; set; } = "";

    [JsonPropertyName("order_ticket")]
    public long OrderTicket { get; set; }

    [JsonPropertyName("price")]
    public double Price { get; set; }

    [JsonPropertyName("volume")]
    public double Volume { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class CloseOrderRequest
{
    [JsonPropertyName("position_ticket")]
    public long PositionTicket { get; set; }
}

public class ModifyOrderRequest
{
    [JsonPropertyName("position_ticket")]
    public long PositionTicket { get; set; }

    [JsonPropertyName("sl")]
    public double? Sl { get; set; }

    [JsonPropertyName("tp")]
    public double? Tp { get; set; }
}

public class AccountInfo
{
    [JsonPropertyName("login")]
    public long Login { get; set; }

    [JsonPropertyName("balance")]
    public double Balance { get; set; }

    [JsonPropertyName("equity")]
    public double Equity { get; set; }

    [JsonPropertyName("profit")]
    public double Profit { get; set; }

    [JsonPropertyName("margin")]
    public double Margin { get; set; }

    [JsonPropertyName("margin_free")]
    public double MarginFree { get; set; }

    [JsonPropertyName("margin_level")]
    public double MarginLevel { get; set; }

    [JsonPropertyName("leverage")]
    public int Leverage { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";

    [JsonPropertyName("server")]
    public string Server { get; set; } = "";

    [JsonPropertyName("trade_allowed")]
    public bool TradeAllowed { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class PositionInfo
{
    [JsonPropertyName("ticket")]
    public long Ticket { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = ""; // "buy" or "sell"

    [JsonPropertyName("volume")]
    public double Volume { get; set; }

    [JsonPropertyName("price_open")]
    public double PriceOpen { get; set; }

    [JsonPropertyName("sl")]
    public double Sl { get; set; }

    [JsonPropertyName("tp")]
    public double Tp { get; set; }

    [JsonPropertyName("price_current")]
    public double PriceCurrent { get; set; }

    [JsonPropertyName("profit")]
    public double Profit { get; set; }

    [JsonPropertyName("swap")]
    public double Swap { get; set; }

    [JsonPropertyName("commission")]
    public double Commission { get; set; }

    [JsonPropertyName("magic")]
    public int Magic { get; set; }

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";

    [JsonPropertyName("time")]
    public string Time { get; set; } = "";
}

public class SymbolInfo
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "";

    [JsonPropertyName("bid")]
    public double Bid { get; set; }

    [JsonPropertyName("ask")]
    public double Ask { get; set; }

    [JsonPropertyName("spread")]
    public int Spread { get; set; }

    [JsonPropertyName("digits")]
    public int Digits { get; set; }
}
