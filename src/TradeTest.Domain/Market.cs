namespace TradeTest.Domain;

/// <summary>Prices are raw exchange prices; adjusted series must declare a separate method/version.</summary>
public sealed record MarketBar(
    string SecurityId,
    DateTimeOffset StartsAt,
    TimeSpan Interval,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    bool IsComplete = true)
{
    public DateTimeOffset EndsAt => StartsAt + Interval;
}

public sealed record MarketQuote(
    string SecurityId,
    DateTimeOffset ObservedAt,
    decimal Bid,
    decimal Ask)
{
    public decimal SpreadBps => Bid > 0 && Ask >= Bid ? (Ask - Bid) / ((Ask + Bid) / 2m) * 10_000m : decimal.MaxValue;
}

public enum DataIssueCode
{
    MissingIdentifier,
    InvalidInterval,
    InvalidPrice,
    InvalidOhlc,
    InvalidVolume,
    DuplicateBar,
    OverlappingBar,
    MissingBar,
    IncompleteBar,
    FutureBar,
    StaleQuote
}

public sealed record DataIssue(DataIssueCode Code, string Detail);
