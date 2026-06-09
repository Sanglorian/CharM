using System.Globalization;
using System.Text;

namespace CharM.Engine.Economy;

/// <summary>
/// Light money model mirroring the OCB <c>D20Currency</c>: five denominations
/// (copper, silver, gold, platinum, astral diamond) with copper exchange rates
/// <c>{1, 10, 100, 10000, 1000000}</c> — i.e. 1 gp = 100 cp, 1 pp = 100 gp,
/// 1 ad = 10,000 gp.
/// </summary>
/// <remarks>
/// Money is stored, never recomputed, on character load: existing files round
/// trip their money strings verbatim. This type is used when creating new
/// characters and when applying buy/add edits, so it favours exact
/// denomination preservation over forcing everything into gold.
/// </remarks>
public readonly struct D20Currency : IEquatable<D20Currency>, IComparable<D20Currency>
{
    /// <summary>Copper value of one unit of each denomination, lowest to highest.</summary>
    public static readonly long[] RatesInCopper = [1, 10, 100, 10_000, 1_000_000];

    /// <summary>Canonical denomination abbreviations, aligned to <see cref="RatesInCopper"/>.</summary>
    public static readonly string[] Abbreviations = ["cp", "sp", "gp", "pp", "ad"];

    public double Copper { get; init; }
    public double Silver { get; init; }
    public double Gold { get; init; }
    public double Platinum { get; init; }
    public double Astral { get; init; }

    public static readonly D20Currency Zero = default;

    public D20Currency(double copper, double silver, double gold, double platinum, double astral)
    {
        Copper = copper;
        Silver = silver;
        Gold = gold;
        Platinum = platinum;
        Astral = astral;
    }

    /// <summary>Total value expressed in copper pieces (may be negative or fractional).</summary>
    public double TotalCopper =>
        Copper * RatesInCopper[0]
        + Silver * RatesInCopper[1]
        + Gold * RatesInCopper[2]
        + Platinum * RatesInCopper[3]
        + Astral * RatesInCopper[4];

    /// <summary>Total value expressed in gold pieces (may be fractional/negative).</summary>
    public double ToGold() => TotalCopper / RatesInCopper[2];

    /// <summary>True when the net value is below zero copper.</summary>
    public bool IsNegative => TotalCopper < 0;

    /// <summary>True when the net value is exactly zero copper.</summary>
    public bool IsZero => TotalCopper == 0;

    /// <summary>Construct a currency holding a whole-gold amount.</summary>
    public static D20Currency FromGold(double gold) => new() { Gold = gold };

    /// <summary>Construct a currency from a raw copper total.</summary>
    public static D20Currency FromCopper(double copper) => new() { Copper = copper };

    /// <summary>
    /// Carry denominations up so each holds the largest whole unit possible
    /// (e.g. 150 cp → 1 gp 5 sp). Fractional remainders settle in copper.
    /// Negative totals normalize to a single negative copper amount.
    /// </summary>
    public D20Currency Normalize()
    {
        var total = TotalCopper;
        if (total < 0)
            return FromCopper(total);

        // Settle from the highest denomination down.
        var remaining = total;
        double astral = Math.Floor(remaining / RatesInCopper[4]);
        remaining -= astral * RatesInCopper[4];
        double platinum = Math.Floor(remaining / RatesInCopper[3]);
        remaining -= platinum * RatesInCopper[3];
        double gold = Math.Floor(remaining / RatesInCopper[2]);
        remaining -= gold * RatesInCopper[2];
        double silver = Math.Floor(remaining / RatesInCopper[1]);
        remaining -= silver * RatesInCopper[1];
        double copper = remaining; // whatever is left, including any fraction

        return new D20Currency(copper, silver, gold, platinum, astral);
    }

    public static D20Currency operator +(D20Currency a, D20Currency b) => new(
        a.Copper + b.Copper,
        a.Silver + b.Silver,
        a.Gold + b.Gold,
        a.Platinum + b.Platinum,
        a.Astral + b.Astral);

    public static D20Currency operator -(D20Currency a, D20Currency b) => new(
        a.Copper - b.Copper,
        a.Silver - b.Silver,
        a.Gold - b.Gold,
        a.Platinum - b.Platinum,
        a.Astral - b.Astral);

    public static D20Currency operator *(D20Currency a, int factor) => new(
        a.Copper * factor,
        a.Silver * factor,
        a.Gold * factor,
        a.Platinum * factor,
        a.Astral * factor);

    public int CompareTo(D20Currency other) => TotalCopper.CompareTo(other.TotalCopper);

    public static bool operator <(D20Currency a, D20Currency b) => a.TotalCopper < b.TotalCopper;
    public static bool operator >(D20Currency a, D20Currency b) => a.TotalCopper > b.TotalCopper;
    public static bool operator <=(D20Currency a, D20Currency b) => a.TotalCopper <= b.TotalCopper;
    public static bool operator >=(D20Currency a, D20Currency b) => a.TotalCopper >= b.TotalCopper;

    public bool Equals(D20Currency other) => TotalCopper == other.TotalCopper;
    public override bool Equals(object? obj) => obj is D20Currency c && Equals(c);
    public override int GetHashCode() => TotalCopper.GetHashCode();
    public static bool operator ==(D20Currency a, D20Currency b) => a.Equals(b);
    public static bool operator !=(D20Currency a, D20Currency b) => !a.Equals(b);

    /// <summary>
    /// Parse an OCB money string such as <c>"100 gp"</c>, <c>"125,000 gp"</c>,
    /// or a multi-denomination string like <c>"5 gp 3 sp 2 cp"</c>. A bare
    /// number with no unit (e.g. a Residuum value) is interpreted as gold.
    /// Unrecognised input yields <see cref="Zero"/>.
    /// </summary>
    public static D20Currency Parse(string? text)
    {
        TryParse(text, out var result);
        return result;
    }

    public static bool TryParse(string? text, out D20Currency result)
    {
        result = Zero;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var amounts = new double[RatesInCopper.Length];
        bool any = false;
        double? pendingNumber = null;

        // Tokenise allowing "1,250gp" or "1,250 gp": strip commas, then walk
        // tokens pairing numbers with following unit abbreviations.
        var cleaned = text.Replace(",", string.Empty);
        var parts = cleaned.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in parts)
        {
            var part = raw.Trim();
            if (part.Length == 0)
                continue;

            // A unit abbreviation following a pending number.
            int unitIdx = Array.FindIndex(Abbreviations, a => string.Equals(a, part, StringComparison.OrdinalIgnoreCase));
            if (unitIdx >= 0)
            {
                if (pendingNumber.HasValue)
                {
                    amounts[unitIdx] += pendingNumber.Value;
                    pendingNumber = null;
                    any = true;
                }
                continue;
            }

            // A combined "1250gp" form: split trailing letters.
            int splitAt = part.Length;
            while (splitAt > 0 && (char.IsLetter(part[splitAt - 1])))
                splitAt--;
            if (splitAt > 0 && splitAt < part.Length)
            {
                var numPart = part[..splitAt];
                var unitPart = part[splitAt..];
                int ui = Array.FindIndex(Abbreviations, a => string.Equals(a, unitPart, StringComparison.OrdinalIgnoreCase));
                if (ui >= 0 && double.TryParse(numPart, NumberStyles.Any, CultureInfo.InvariantCulture, out var combined))
                {
                    amounts[ui] += combined;
                    any = true;
                    continue;
                }
            }

            if (double.TryParse(part, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
            {
                // Flush a previous bare number (no unit) as gold before storing the new one.
                if (pendingNumber.HasValue)
                {
                    amounts[2] += pendingNumber.Value; // bare number => gold
                    any = true;
                }
                pendingNumber = num;
            }
        }

        if (pendingNumber.HasValue)
        {
            amounts[2] += pendingNumber.Value; // trailing bare number => gold
            any = true;
        }

        result = new D20Currency(amounts[0], amounts[1], amounts[2], amounts[3], amounts[4]);
        return any;
    }

    /// <summary>
    /// Render to a canonical OCB-style string. OCB stores money gold-denominated
    /// (it never auto-promotes to platinum/astral), so platinum and astral fold
    /// down into gold. Zero renders as <c>"0 gp"</c>; sub-gold remainders render
    /// as silver/copper (e.g. <c>"5 gp 3 sp"</c>); negatives render as negative
    /// gold.
    /// </summary>
    public override string ToString()
    {
        var total = TotalCopper;
        if (total == 0)
            return "0 gp";

        if (total < 0)
            return FormatNumber(total / (double)RatesInCopper[2]) + " gp";

        double gp = Math.Floor(total / RatesInCopper[2]);
        double remainder = total - gp * RatesInCopper[2];
        double sp = Math.Floor(remainder / RatesInCopper[1]);
        remainder -= sp * RatesInCopper[1];
        double cp = remainder;

        var sb = new StringBuilder();
        AppendPiece(sb, gp, "gp");
        AppendPiece(sb, sp, "sp");
        AppendPiece(sb, cp, "cp");
        return sb.Length == 0 ? "0 gp" : sb.ToString();
    }

    private static void AppendPiece(StringBuilder sb, double amount, string abbr)
    {
        if (amount == 0)
            return;
        if (sb.Length > 0)
            sb.Append(' ');
        sb.Append(FormatNumber(amount)).Append(' ').Append(abbr);
    }

    private static string FormatNumber(double value)
    {
        // Whole numbers use thousands separators; fractional keeps decimals.
        return value == Math.Floor(value)
            ? ((long)value).ToString("#,0", CultureInfo.InvariantCulture)
            : value.ToString("#,0.##", CultureInfo.InvariantCulture);
    }
}
