using CharM.Engine.CharacterModel;
using CharM.Engine.Evaluation;
using CharM.Engine.Prerequisites;
using CharM.Engine.Rules;

namespace CharM.Engine.Orchestration;

/// <summary>
/// Orchestrates character building by replaying build choices through the rules engine.
/// Executes all directives in two phases (skeleton then full) and produces computed stats.
/// </summary>
public sealed partial class CharacterBuilder
{
    private readonly Func<string, RulesElement?> _findById;
    private readonly Func<string, string, RulesElement?> _findByNameAndType;

    /// <summary>Tracks all stat names created during build for iteration.</summary>
    private readonly HashSet<string> _knownStatNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tracks visited element IDs during recursive directive execution to prevent cycles.</summary>
    private readonly HashSet<string> _visitedElements = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Grants whose requires condition failed during initial processing, to be retried later.</summary>
    private readonly List<(GrantDirective Grant, CharacterElement Parent, int Level)> _deferredGrants = [];

    /// <summary>
    /// Grants tagged with <c>level="N"</c> > current character level when first encountered,
    /// to be fired when the build reaches that level. Keyed by target level. Powers
    /// per-level scaling for companions (Cat 2 → Cat 30), paragon paths (path features
    /// at L12/16/20), themes, and any other element with embedded leveled grants.
    /// </summary>
    private readonly SortedDictionary<int, List<(GrantDirective Grant, CharacterElement Parent)>> _futureLevelGrants = new();

    /// <summary>
    /// Elements collected during phase 1 (skeleton) that need phase 2 (computation).
    /// Each entry is (RulesElement, CharacterElement parent, characterLevel).
    /// </summary>
    private readonly List<(RulesElement Element, CharacterElement Parent, int Level)> _pendingPhase2 = [];

    /// <summary>
    /// Tracks equipment categories currently worn/equipped (e.g., "armor:heavy", "armor:chain", "armor:").
    /// Used to evaluate wearing/not-wearing conditions on statadds.
    /// </summary>
    private readonly HashSet<string> _equippedCategories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// RulesElements dropped from the active tree by an
    /// <see cref="ElementReplacement"/> swap (multiclass-feat power swaps,
    /// retraining, etc.). They no longer contribute statadds or sit in the
    /// element tree, but OCB still emits them in <c>PowerStats</c> so the
    /// user can re-train back. <see cref="ImportExport.PowerStatsBuilder"/>
    /// surfaces them as extra power cards alongside cascaded magic-item
    /// grants.
    /// </summary>
    private readonly List<RulesElement> _replacedElements = [];

    private readonly List<AppliedReplacement> _appliedReplacements = [];

    /// <summary>Read-only view of <see cref="_replacedElements"/>.</summary>
    public IReadOnlyList<RulesElement> ReplacedElements => _replacedElements;

    /// <summary>
    /// Tracks <see cref="SuggestDirective"/> grants per slot owner.
    /// Key = the InternalId of the element that ran the suggest directive
    /// (the "slot owner" — e.g. a Class). Value = ordered list of suggested
    /// (InternalId, ElementType) targets the engine recorded for that owner.
    /// Surfaced by the wizard's candidate pipeline so the picker can float
    /// matches to the top with a "Recommended" badge.
    /// </summary>
    private readonly Dictionary<string, List<(string InternalId, string ElementType)>> _suggestionsBySlotOwner =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Read-only view of suggested elements collected during phase 1, keyed
    /// by the InternalId of the suggesting element (matches
    /// <see cref="ChoiceSlot.OwnerInternalId"/>).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<(string InternalId, string ElementType)>> SuggestionsBySlotOwner
        => _suggestionsBySlotOwner.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<(string, string)>)kv.Value,
            StringComparer.OrdinalIgnoreCase);

    public CharacterElementTree ElementTree { get; } = new();
    public StatBlock Stats { get; } = new();
    public ModifyOverlay Overlay { get; } = new();

    /// <summary>
    /// Create a builder with rules database lookup functions.
    /// </summary>
    public CharacterBuilder(
        Func<string, RulesElement?> findById,
        Func<string, string, RulesElement?> findByNameAndType)
    {
        _findById = findById;
        _findByNameAndType = findByNameAndType;

        ElementTree.ElementResolver = (name, type) =>
            _findById(name) ?? _findByNameAndType(name, type);
    }

    /// <summary>
    /// Build a character by replaying all build choices through the rules engine.
    /// </summary>
    /// <param name="baseAbilityScores">Pre-racial ability scores (e.g., Strength → 16).</param>
    /// <param name="buildChoices">Per-level build choices, keyed by level number.</param>
    /// <param name="elementTally">Optional: flat list of all active elements from .dnd4e RulesElementTally.</param>
    /// <param name="equippedItems">Optional: list of equipped item internal IDs to process as stat contributors.</param>
    /// <param name="replacements">Optional: per-level retraining swaps (drop old element, grant new element). Applied after the level's normal choices are processed.</param>
    /// <param name="inventoryDirectiveItems">Optional: inventory items (count > 0) that carry GrantDirective / StatAddDirective rules and need to fire from the bag (boons, Wondrous Items like Bag of Holding / Spyglass of Perception, Deck of Many Things cards, etc.). Phase1 runs but their categories are NOT registered for wearing checks.</param>
    public void Build(
        IReadOnlyDictionary<string, int> baseAbilityScores,
        IReadOnlyDictionary<int, IReadOnlyList<ElementChoice>> buildChoices,
        IReadOnlyList<ElementChoice>? elementTally = null,
        IReadOnlyList<ElementChoice>? equippedItems = null,
        IReadOnlyDictionary<int, IReadOnlyList<ElementReplacement>>? replacements = null,
        IReadOnlyList<ElementChoice>? inventoryDirectiveItems = null)
    {
        // 1. Set base ability scores
        foreach (var (name, value) in baseAbilityScores)
        {
            TrackStat(name);
            var stat = Stats.GetOrCreateStat(name);
            stat.AddContribution(new StatContribution { Value = value });
        }

        // 2. Phase 1 (skeleton): process each level's build choices
        foreach (var (level, choices) in buildChoices.OrderBy(kv => kv.Key))
        {
            ProcessLevelSkeleton(level, choices);

            if (replacements is not null
                && replacements.TryGetValue(level, out var levelReplacements)
                && levelReplacements.Count > 0)
            {
                ApplyReplacements(level, levelReplacements);
            }
        }

        // 3. Process supplemental elements from the tally that weren't reached
        if (elementTally is not null)
        {
            ProcessTallySupplement(elementTally);
        }

        // 4. Process equipped items (armor, weapons, magic items)
        if (equippedItems is not null)
        {
            ProcessEquipment(equippedItems);
        }

        // 4a. Process inventory items whose Rules contain a GrantDirective
        //     or StatAddDirective. 
        if (inventoryDirectiveItems is not null)
        {
            ProcessInventoryDirectives(inventoryDirectiveItems);
        }

        // 4.5. PHB3 hybrid armor proficiency rule: a hybrid character only
        //      gets the armor / shield proficiencies that are common to BOTH
        //      hybrid classes' grant lists. The data ships each hybrid grant
        //      element with its full per-class proficiency set, so we
        //      reconcile after both grants are present.
        ReconcileHybridArmorProficiencies();

        // 5. Compute character max level
        int maxCharLevel = buildChoices.Count > 0 ? buildChoices.Keys.Max() : 1;

        // 6. Phase 2 (computation) for all collected elements
        //    Use the character's max level for requires checks (e.g., "11 level")
        //    since tier-scaling feats acquired at level 1 should still get their
        //    Paragon/Epic bonuses when the character is high enough level.
        ExecuteAllPhase2(maxCharLevel);

        // 6a. OCB Sets pass: activate Item Set Benefit elements whose
        //     piece-count threshold is met by owned set members. Runs
        //     after Phase 2 because the per-member "<setId> Set Count"
        //     StatAddDirective is a Phase-2 directive — Set Count is
        //     not populated until ExecuteAllPhase2 completes. Activated
        //     benefits queue their own Phase 2 work; a second
        //     ExecuteAllPhase2 drains those entries. See
        //     engine-special-cases.md §14.
        ApplyItemSetBenefits();
        ExecuteAllPhase2(maxCharLevel);

        // 6.5. Native psionic engine special case. OCB runs this after normal
        //      rules execution so _SuppressAugments overlays from multiclass
        //      power picks are visible, but Psionic Dabbler replace picks remain
        //      At-Will because its nameless modify does not target replace rows.
        ApplyPsionicPowerPointSpecialCases();

        // 7. Index trained weapons/implements from active proficiency elements.
        //    "Weapon Proficiency (Longsword)" → "Longsword" in TrainedWeapons.
        //    Category-level grants like "Military Melee" already expanded into
        //    per-weapon Proficiency elements via GrantDirective during phase 1,
        //    so a single pass over active Proficiency elements suffices.
        IndexProficiencies();

        // 8. Index "Ability Choice" elements (e.g. Eldritch Blast Charisma) into
        //    Stats.ChosenAbilities. Powers whose Attack/Hit text reads
        //    "X or Y modifier" use this to pick the player-selected ability
        //    instead of silently defaulting to whichever happens to have the
        //    higher modifier.
        IndexAbilityChoices();

        // 9. Index every class id the character has, has via Hybrid Class, or
        //    counts as (CountsAsClass _SupportsID — both inherent and multiclass
        //    feat grants). PowerStatCalculator uses this to answer "is this
        //    power one of mine?" without parsing the Display string.
        IndexClassEquivalents();
    }

}
