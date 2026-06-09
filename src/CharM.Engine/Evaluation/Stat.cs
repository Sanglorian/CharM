using CharM.Engine.Rules;

namespace CharM.Engine.Evaluation;

/// <summary>
/// A single named stat (e.g., "AC", "Speed", "Strength") with a list of contributions.
/// Implements bonus stacking: same-type bonuses don't stack (highest wins), untyped always stack.
/// </summary>
public sealed class Stat
{
    /// <summary>Sentinel value meaning "this bonus type is zeroed out — ignore all further contributions of this type".</summary>
    private const int ZeroedSentinel = 10000;

    private readonly List<StatContribution> _contributions = [];

    public Stat(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public IReadOnlyList<StatContribution> Contributions => _contributions;

    public void AddContribution(StatContribution contribution)
    {
        _contributions.Add(contribution);
    }

    public void ClearContributions()
    {
        _contributions.Clear();
    }

    /// <summary>
    /// Compute the final stat value using stacking rules.
    /// 
    /// Algorithm:
    /// 1. For each active contribution:
    ///    - Untyped (BonusType is null/empty): always accumulate
    ///    - Typed positive: only the highest bonus of each type applies
    ///    - Typed negative (penalty): worst penalty of each type applies
    ///    - If a type has both positive and negative, only the positive counts
    /// 2. Zero/NonZero bucket logic:
    ///    - Contributions with Zero=true go into zero_sum
    ///    - Contributions with NonZero=true go into non_zero_sum
    ///    - Final: result = main + (main != 0 ? non_zero_sum : zero_sum)
    /// </summary>
    public int ComputeValue(StatBlock block)
    {
        int mainSum = 0;
        int zeroSum = 0;
        int nonZeroSum = 0;

        // Track the highest bonus (or worst penalty) per typed bonus
        var typeMaxMain = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var typeMaxZero = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var typeMaxNonZero = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Half-point literal accumulator. Each contribution flagged half-point="true"
        // with a literal value represents value+0.5 (or value-0.5 if negative): the
        // stored integer is the value rounded toward zero. When two such contributions
        // combine (typically two halves of a hybrid class), the implicit halves
        // combine to an additional ±1. 
        // We track positive and negative contribs separately so mixed signs
        // round correctly.
        int posHalfCount = 0;
        int negHalfCount = 0;

        foreach (var c in _contributions)
        {
            // Conditional contributions (with Condition text like "against poison")
            // are display-only — they don't contribute to the base stat value.
            // They're stored for power card/character sheet display.
            if (!string.IsNullOrEmpty(c.Condition))
                continue;

            // Inactive contributions (gating predicate not satisfied —
            // requires/wearing/not-wearing) are kept for export round-trip
            // but excluded from value math.
            if (!c.Active)
                continue;

            // String-payload contributions (textstring directives) are
            // display-only — their numeric value is always 0.
            if (c.StringPayload is not null)
                continue;

            int amount = c.GetEffectiveValue(block);
            bool isAbilModExpr =
                c.Expression is ValueExpression.AbilityModifier
                             or ValueExpression.AbilityModFunction;

            if (c.HalfPoint && isAbilModExpr)
            {
                // Legacy abilmod halving: "half your <ability> modifier" — the
                // expression yields the full modifier and we halve it here.
                // (No real data uses this combination today, but keep the path
                // in case a future XML directive does.)
                amount /= 2;
            }
            else if (c.HalfPoint)
            {
                // Literal half-point: track the implicit half for combination
                // at the end. The integer value still accumulates normally.
                if (amount > 0) posHalfCount++;
                else if (amount < 0) negHalfCount++;
            }

            bool isPenalty = amount < 0;

            if (c.Zero)
                AccumulateWithStacking(ref zeroSum, typeMaxZero, c.BonusType, amount, isPenalty);
            else if (c.NonZero)
                AccumulateWithStacking(ref nonZeroSum, typeMaxNonZero, c.BonusType, amount, isPenalty);
            else
                AccumulateWithStacking(ref mainSum, typeMaxMain, c.BonusType, amount, isPenalty);
        }

        // Combine paired half-points. Each pair of positive halves adds +1;
        // each pair of negative halves subtracts 1. Unpaired halves drop.
        mainSum += (posHalfCount / 2) - (negHalfCount / 2);

        int result = mainSum;
        if (mainSum != 0)
            result += nonZeroSum;
        else
            result += zeroSum;

        return result;
    }

    /// <summary>
    /// Applies the stacking logic for a single contribution.
    /// Untyped bonuses always accumulate. Typed bonuses: highest positive wins;
    /// if only penalties exist for a type, worst penalty applies.
    /// Sentinel value 10000 means "zeroed out" — skip this type entirely.
    /// </summary>
    private static void AccumulateWithStacking(
        ref int sum,
        Dictionary<string, int> typeMax,
        string? bonusType,
        int amount,
        bool isPenalty)
    {
        if (string.IsNullOrEmpty(bonusType))
        {
            // Untyped: always accumulate
            sum += amount;
            return;
        }

        if (!typeMax.TryGetValue(bonusType, out int currentMax))
        {
            // First time seeing this type
            typeMax[bonusType] = amount;
            sum += amount;
            return;
        }

        // Sentinel: this type has been zeroed out
        if (currentMax == ZeroedSentinel)
            return;

        if (!isPenalty)
        {
            // Positive bonus
            if (currentMax <= 0)
            {
                // Previously only had penalties; the positive replaces them
                sum += amount - currentMax;
                typeMax[bonusType] = amount;
            }
            else if (amount > currentMax)
            {
                // New positive is higher than old positive
                sum += amount - currentMax;
                typeMax[bonusType] = amount;
            }
            // else: amount <= currentMax positive, no change
        }
        else
        {
            // Penalty (amount < 0)
            if (currentMax >= 0)
            {
                // There's already a non-negative bonus (including +0) — penalty doesn't apply.
                // A +0 typed bonus explicitly cancels penalties of the same type.
            }
            else if (amount < currentMax)
            {
                // Worse penalty (more negative)
                sum += amount - currentMax;
                typeMax[bonusType] = amount;
            }
            // else: existing penalty is worse or equal, no change
        }
    }
}
