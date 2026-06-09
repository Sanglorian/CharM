using System.Text;
using System.Text.RegularExpressions;
using CharM.Engine.Evaluation;
using CharM.Engine.Rules;

namespace CharM.Engine.Powers;

/// <summary>
/// Computed stats for a single power, potentially with a specific weapon.
/// </summary>
public sealed class PowerStatBlock
{
    /// <summary>Total attack bonus (ability mod + half-level + proficiency + enhancement + keyword bonuses)</summary>
    public int AttackBonus { get; set; }

    /// <summary>Attack bonus breakdown for display ("+3 Strength modifier.\n+1 half level.\n")</summary>
    public string AttackComponents { get; set; } = "";

    /// <summary>Damage expression string (e.g., "1d8+5")</summary>
    public string DamageExpression { get; set; } = "";

    /// <summary>Comma-separated damage type keywords (e.g., "Fire, Radiant")</summary>
    public string DamageType { get; set; } = "";

    /// <summary>Damage bonus breakdown for display</summary>
    public string DamageComponents { get; set; } = "";

    /// <summary>Defense targeted (e.g., "AC", "Reflex")</summary>
    public string? Defense { get; set; }

    /// <summary>Conditional bonuses text (e.g., "+2 to attack when bloodied")</summary>
    public string Conditions { get; set; } = "";

    /// <summary>Healing value (for healing powers)</summary>
    public int? HealingValue { get; set; }

    /// <summary>Healing text for powers that add hit points.</summary>
    public string Healing { get; set; } = "";

    /// <summary>Healing bonus breakdown for display.</summary>
    public string HealingComponents { get; set; } = "";

    /// <summary>Power keywords from the power element</summary>
    public List<string> Keywords { get; } = [];

    /// <summary>
    /// The ability that was actually used for the attack roll after resolving
    /// "X or Y" choice constructs (warlock pact, etc.). Populated even for
    /// single-ability attacks. Empty when the power has no attack line. This
    /// is what OCB writes into the <c>&lt;AttackStat&gt;</c> child of the
    /// power's <c>&lt;Weapon&gt;</c> entry.
    /// </summary>
    public string ResolvedAttackStat { get; set; } = string.Empty;
}
