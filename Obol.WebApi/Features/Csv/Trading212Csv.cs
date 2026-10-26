using System.Globalization;
using System.Text;
using Obol.Library.Enums;
using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Instruments.Services;
using Obol.WebApi.Features.Portfolio.Services;
using Obol.WebApi.Features.Trading212;

namespace Obol.WebApi.Features.Csv;

/// <summary>
/// Reads a Trading 212 CSV export (History › Export): buys, sells, dividends, deposits, withdrawals, interest and fees.
/// Goes by the column names, so older exports ("Total (EUR)") and newer ones ("Total" + "Currency (Total)", "Time (UTC)")
/// both work; lines it doesn't keep (currency conversions, splits) are counted.
/// </summary>
internal static class Trading212Csv
{
    private static readonly string[] TimeFormats =
    [
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss.ff", "yyyy-MM-dd HH:mm:ss.f", "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss.fff", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", "yyyy-MM-dd HH:mm", "dd/MM/yyyy HH:mm:ss",
    ];

    private static readonly string[] FeeColumns = ["conversion fee", "transaction tax", "stamp duty", "finra fee", "transaction fee", "ptm levy", "charge amount"];

    /// <summary>
    /// Reads an export.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <param name="defaultCurrency">The account currency when the file doesn't say.</param>
    /// <returns>What was read, or null when it isn't a Trading 212 export.</returns>
    public static ImportBatch? Parse(string text, string defaultCurrency)
    {
        var rows = Rows(text).ToList();
        if (rows.Count == 0)
        {
            return null;
        }

        var header = rows[0].Select(static name => name.Trim().ToLowerInvariant()).ToList();
        int Column(params string[] names)
        {
            foreach (var name in names)
            {
                var index = header.IndexOf(name);
                if (index >= 0)
                {
                    return index;
                }
            }

            return -1;
        }

        int StartsWith(string prefix) => header.FindIndex(name => name.StartsWith(prefix, StringComparison.Ordinal));

        var action = Column("action");
        var time = Column("time", "time (utc)");
        if (action < 0 || time < 0)
        {
            return null;
        }

        var isin = Column("isin");
        var ticker = Column("ticker");
        var name = Column("name");
        var shares = Column("no. of shares", "quantity");
        var price = Column("price / share", "price per share");
        var priceCurrency = Column("currency (price / share)");
        var total = Column("total");
        if (total < 0)
        {
            total = StartsWith("total (");
        }

        var totalCurrency = Column("currency (total)");
        var totalHeaderCurrency = total >= 0 ? InParentheses(header[total]) : null;
        var result = Column("result");
        if (result < 0)
        {
            result = StartsWith("result (");
        }

        var withholding = Column("withholding tax");
        var withholdingCurrency = Column("currency (withholding tax)");
        var notes = Column("notes");
        var merchant = Column("merchant name");
        var id = Column("id");
        var fees = header.Select((column, index) => (column, index))
            .Where(pair => FeeColumns.Any(fee => pair.column.Contains(fee, StringComparison.Ordinal)) && !pair.column.StartsWith("currency (", StringComparison.Ordinal))
            .Select(static pair => pair.index)
            .ToList();

        var batch = new ImportBatch();
        foreach (var row in rows.Skip(1))
        {
            string Cell(int index) => index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;
            decimal? Number(int index) => decimal.TryParse(Cell(index), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

            var what = Cell(action).ToLowerInvariant();
            if (what.Length == 0)
            {
                continue;
            }

            if (ParseTime(Cell(time)) is not { } at)
            {
                batch.Skip("line with an unreadable time");
                continue;
            }

            var currency = Trading212Mapper.Currency(Cell(totalCurrency) is { Length: 3 } own ? own : totalHeaderCurrency ?? defaultCurrency);
            batch.Currency ??= Cell(totalCurrency) is { Length: 3 } || totalHeaderCurrency is not null ? currency : null;
            var amount = Number(total) ?? 0m;
            var note = Cell(notes);

            if (what.Contains("buy", StringComparison.Ordinal) || what.Contains("sell", StringComparison.Ordinal))
            {
                var instrument = Instrument(Cell(isin), Cell(ticker), Cell(name), Cell(priceCurrency), currency);
                var quantity = Math.Abs(Number(shares) ?? 0m);
                var unit = Number(price) ?? 0m;
                if (instrument is null || quantity == 0 || unit <= 0)
                {
                    batch.Skip("trade without shares or price");
                    continue;
                }

                var buy = what.Contains("buy", StringComparison.Ordinal);
                var fee = fees.Sum(index => Math.Abs(Number(index) ?? 0m));
                batch.Add(instrument);
                batch.Trades.Add(new ImportedTrade(
                    instrument.Key, buy, quantity, unit, instrument.Currency, null,
                    amount != 0 ? Math.Abs(amount) : quantity * unit, buy ? null : Number(result), fee, at, DataSource.Csv, NullIfEmpty(Cell(id))));
            }
            else if (what.StartsWith("dividend", StringComparison.Ordinal))
            {
                var instrument = Instrument(Cell(isin), Cell(ticker), Cell(name), Cell(priceCurrency), currency);
                if (instrument is null || amount == 0)
                {
                    batch.Skip("dividend without an instrument");
                    continue;
                }

                var held = Number(shares) is { } count && count != 0 ? Math.Abs(count) : (decimal?)null;
                var perShare = Number(price);
                batch.Add(instrument);
                batch.Dividends.Add(new ImportedDividend(
                    instrument.Key, at, held, perShare, amount,
                    Withheld(Number(withholding), Cell(withholdingCurrency), currency, instrument.Currency, amount, held, perShare),
                    (InParentheses(what) ?? "dividend").ToUpperInvariant(), DataSource.Csv));
            }
            else if (CashKindOf(what) is { } kind)
            {
                if (amount == 0)
                {
                    batch.Skip(what);
                    continue;
                }

                var signed = kind switch
                {
                    CashKind.Deposit or CashKind.Interest => Math.Abs(amount),
                    CashKind.Withdrawal or CashKind.Fee => -Math.Abs(amount),
                    _ => what.Contains("debit", StringComparison.Ordinal) ? -Math.Abs(amount) : amount,
                };

                // Card payments are money leaving the account (like a withdrawal, so they never look like a loss), named
                // after the shop; bank transfers keep the note without the account number.
                var label = what.StartsWith("card ", StringComparison.Ordinal) && Cell(merchant) is { Length: > 0 } shop ? "Card · " + shop
                    : note.StartsWith("Sent to Bank Account", StringComparison.OrdinalIgnoreCase) ? "To your bank account"
                    : note.StartsWith("Bank Transfer", StringComparison.OrdinalIgnoreCase) ? "Bank transfer"
                    : note.Length > 0 ? note : Title(what);
                batch.Cash.Add(new ImportedCash(kind, signed, at, label, DataSource.Csv));
            }
            else
            {
                batch.Skip(what.Contains("split", StringComparison.Ordinal) ? "stock split" : what);
            }
        }

        return batch;
    }

    /// <summary>
    /// The rows of a CSV text (quotes, doubled quotes and line breaks inside quotes handled).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The rows.</returns>
    internal static IEnumerable<List<string>> Rows(string text)
    {
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var start = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(cell.ToString());
                cell.Clear();
                if (row.Any(static value => value.Length > 0))
                {
                    yield return row;
                }

                row = [];
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            if (row.Any(static value => value.Length > 0))
            {
                yield return row;
            }
        }
    }

    private static InstrumentInput? Instrument(string isin, string ticker, string name, string priceCurrency, string accountCurrency)
    {
        var cleanIsin = InstrumentNames.CleanIsin(isin);
        if (cleanIsin is null && ticker.Length == 0)
        {
            return null;
        }

        var displayName = name.Length > 0 ? name : ticker.Length > 0 ? ticker : cleanIsin!;
        return new InstrumentInput(
            InstrumentNames.Key(cleanIsin, ticker),
            cleanIsin,
            InstrumentNames.Truncate(ticker.Length > 0 ? ticker : cleanIsin, 40),
            InstrumentNames.Symbol(ticker.Length > 0 ? ticker : cleanIsin),
            InstrumentNames.Truncate(displayName, 200),
            Trading212Mapper.Currency(priceCurrency.Length > 0 ? priceCurrency : accountCurrency),
            (byte)InstrumentNames.Classify(null, displayName));
    }

    /// <summary>
    /// A Trading 212 time in UTC: "2025-01-03 14:30:12", with milliseconds, with a T, or with the zone at the end
    /// ("2026-01-02 13:21:14+00:00", the newer exports).
    /// </summary>
    /// <param name="text">The cell.</param>
    /// <returns>The time (UTC), or null.</returns>
    internal static DateTime? ParseTime(string text)
    {
        var value = text.Trim();
        if (DateTime.TryParseExact(value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var plain))
        {
            return DateTime.SpecifyKind(plain, DateTimeKind.Utc);
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var zoned)
            ? zoned.UtcDateTime
            : null;
    }

    /// <summary>
    /// The tax withheld at source, in the account currency. Trading 212 gives it in the share's currency (USD for a US
    /// share) next to the net amount per share and the net total in euros, so the rate of that very payment converts it.
    /// </summary>
    internal static decimal? Withheld(decimal? tax, string taxCurrency, string accountCurrency, string instrumentCurrency, decimal amount, decimal? shares, decimal? perShare)
    {
        if (tax is not { } value || value == 0)
        {
            return null;
        }

        value = Math.Abs(value);
        if (taxCurrency.Length == 0 || string.Equals(taxCurrency, accountCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (string.Equals(Trading212Mapper.Currency(taxCurrency), instrumentCurrency, StringComparison.OrdinalIgnoreCase)
            && shares is > 0 && perShare is > 0 && amount > 0)
        {
            var rate = amount / (shares.Value * perShare.Value);
            return Math.Round(value * rate, 2);
        }

        return null;
    }

    private static CashKind? CashKindOf(string action) => action switch
    {
        "deposit" => CashKind.Deposit,
        "withdrawal" => CashKind.Withdrawal,
        "interest on cash" or "lending interest" => CashKind.Interest,
        "new card cost" => CashKind.Fee,
        "card debit" => CashKind.Withdrawal,
        "card credit" or "card refund" => CashKind.Deposit,
        _ when action.StartsWith("interest", StringComparison.Ordinal) => CashKind.Interest,
        _ when action.Contains("cashback", StringComparison.Ordinal) || action.StartsWith("card ", StringComparison.Ordinal)
            || action.StartsWith("transfer", StringComparison.Ordinal) => CashKind.Other,
        _ => null,
    };

    private static string? InParentheses(string text)
    {
        var open = text.IndexOf('(', StringComparison.Ordinal);
        var close = text.LastIndexOf(')');
        return open >= 0 && close > open + 1 ? text[(open + 1)..close].Trim() : null;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : InstrumentNames.Truncate(value, 60);

    private static string Title(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
