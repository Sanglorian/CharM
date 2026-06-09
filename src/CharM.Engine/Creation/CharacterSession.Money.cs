using CharM.Engine.Economy;

namespace CharM.Engine.Creation;

public sealed partial class CharacterSession
{
    // --- Money / light economics ---
    //
    // Money is stored, never recomputed: imported characters keep their money
    // strings verbatim (Details + per-level textstrings). These helpers only
    // touch that state when the user explicitly acts (AutoGold, Buy), so
    // untouched round-trips remain byte-identical.

    private const string CarriedMoneyField = "CarriedMoney";
    private const string StoredMoneyField = "StoredMoney";

    /// <summary>Current Carried Money parsed from the stored Details value.</summary>
    public D20Currency CarriedMoney
        => D20Currency.Parse(Details.GetValueOrDefault(CarriedMoneyField));

    /// <summary>Current Stored (banked) Money parsed from the stored Details value.</summary>
    public D20Currency StoredMoney
        => D20Currency.Parse(Details.GetValueOrDefault(StoredMoneyField));

    /// <summary>
    /// Set Carried Money, writing both the <c>Details/CarriedMoney</c> value and
    /// the matching <c>_PER_LEVEL_{Level}_Carried Money</c> textstring so the
    /// export matches OCB's persistence shape.
    /// </summary>
    public void SetCarriedMoney(D20Currency money)
        => WriteMoney(CarriedMoneyField, "Carried Money", money);

    /// <summary>Set Stored (banked) Money across Details + per-level textstring.</summary>
    public void SetStoredMoney(D20Currency money)
        => WriteMoney(StoredMoneyField, "Stored Money", money);

    private void WriteMoney(string detailField, string perLevelLabel, D20Currency money)
    {
        var text = money.ToString();
        Details[detailField] = text;
        TextStrings[$"_PER_LEVEL_{Level}_{perLevelLabel}"] = text;
        InvalidateSnapshot();
        NotifyChanged();
    }

    /// <summary>
    /// Apply the by-the-book level-appropriate allowance, mirroring OCB's
    /// <c>AutoGold()</c>: Carried Money becomes
    /// <see cref="StartingGoldTable.CarriedGpByLevel"/> for the current level and
    /// Stored Money resets to the default.
    /// </summary>
    /// <remarks>
    /// Levels 2-30 use the starting gold table — see <see cref="StartingGoldTable"/>.
    /// </remarks>
    public void AutoGold()
    {
        SetCarriedMoney(StartingGoldTable.CarriedGpByLevel(Level));
        SetStoredMoney(StartingGoldTable.DefaultStored);
    }
}
