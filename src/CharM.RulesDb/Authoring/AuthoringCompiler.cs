using System.Globalization;
using CharM.Engine.Rules;
using CharM.RulesDb.Import;
using YamlDotNet.RepresentationModel;

namespace CharM.RulesDb.Authoring;

/// <summary>
/// Thrown when authored YAML content is malformed or fails validation.
/// Messages include the source file and (where available) the line number.
/// </summary>
public sealed class AuthoringException(string message) : Exception(message);

/// <summary>Outcome of a compile: element count and any non-fatal warnings.</summary>
public sealed record AuthoringResult(int ElementCount, IReadOnlyList<string> Warnings);

/// <summary>
/// Compiles a tree of human-authored YAML rules content into a CharM
/// <c>rules.db</c>. The YAML maps 1:1 onto <see cref="RulesElement"/>; the
/// resulting <see cref="ParsedElement"/>s are written through the same
/// <see cref="RulesDbBuilder"/> the XML importer uses, so the on-disk format is
/// byte-for-byte compatible with databases the app already loads.
///
/// This exists so an open-content database can be built from Open Game Content
/// without the copyrighted WotC source — see <c>docs/authoring.md</c>.
/// </summary>
public static class AuthoringCompiler
{
    /// <summary>The nine directive discriminator keys recognised under <c>rules:</c>.</summary>
    private static readonly HashSet<string> DirectiveKeys = new(StringComparer.Ordinal)
    {
        "statadd", "statalias", "grant", "drop", "select",
        "replace", "suggest", "modify", "textstring",
    };

    /// <summary>
    /// Parse all YAML under <paramref name="contentRoot"/> (a file or directory),
    /// validate it, and write a fresh SQLite database to <paramref name="dbPath"/>.
    /// </summary>
    public static AuthoringResult Compile(string contentRoot, string dbPath, IProgress<int>? progress = null)
    {
        var elements = LoadElements(contentRoot);
        if (elements.Count == 0)
            throw new AuthoringException($"No rules elements found under '{contentRoot}'.");

        var warnings = Validate(elements);
        RulesDbBuilder.Build(elements, dbPath, progress);
        return new AuthoringResult(elements.Count, warnings);
    }

    /// <summary>Parse and validate all authored content without writing a database.</summary>
    public static AuthoringResult Lint(string contentRoot)
    {
        var elements = LoadElements(contentRoot);
        var warnings = Validate(elements);
        return new AuthoringResult(elements.Count, warnings);
    }

    /// <summary>Parse all authored elements without validating or writing a database.</summary>
    public static List<ParsedElement> LoadElements(string contentRoot)
    {
        var elements = new List<ParsedElement>();
        foreach (var file in EnumerateContentFiles(contentRoot))
            elements.AddRange(ParseFile(file));
        return elements;
    }

    /// <summary>Enumerate <c>*.yaml</c>/<c>*.yml</c> files (recursively) in a deterministic order.</summary>
    public static IEnumerable<string> EnumerateContentFiles(string root)
    {
        if (File.Exists(root))
        {
            yield return root;
            yield break;
        }
        if (!Directory.Exists(root))
            throw new AuthoringException($"Content path not found: '{root}'.");

        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal);
        foreach (var f in files)
            yield return f;
    }

    // -- File / element parsing -------------------------------------------------

    private static IReadOnlyList<ParsedElement> ParseFile(string path)
    {
        var stream = new YamlStream();
        try
        {
            using var text = new StreamReader(path);
            stream.Load(text);
        }
        catch (AuthoringException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AuthoringException($"{Name(path)}: YAML parse error: {ex.Message}");
        }

        var results = new List<ParsedElement>();
        foreach (var doc in stream.Documents)
        {
            if (doc.RootNode is YamlScalarNode { Value: null or "" })
                continue; // empty document
            if (doc.RootNode is not YamlSequenceNode seq)
                throw new AuthoringException(At(path, doc.RootNode, "top level of a content file must be a list of elements."));

            foreach (var item in seq.Children)
            {
                if (item is not YamlMappingNode map)
                    throw new AuthoringException(At(path, item, "each list entry must be a mapping with at least id/name/type."));
                results.Add(ParseElement(path, map));
            }
        }
        return results;
    }

    private static ParsedElement ParseElement(string file, YamlMappingNode map)
    {
        var element = new RulesElement
        {
            InternalId = RequireScalar(file, map, "id"),
            Name = RequireScalar(file, map, "name"),
            Type = RequireScalar(file, map, "type"),
            Source = OptionalScalar(map, "source"),
            Prereqs = OptionalScalar(map, "prereqs"),
            FieldEntries = ParseFields(file, map),
            Rules = ParseRules(file, map),
        };
        return new ParsedElement(element, ParseCategories(file, map));
    }

    private static IReadOnlyList<KeyValuePair<string, string>> ParseFields(string file, YamlMappingNode map)
    {
        var list = new List<KeyValuePair<string, string>>();
        if (!TryGet(map, "fields", out var node))
            return list;
        if (node is not YamlMappingNode fields)
            throw new AuthoringException(At(file, node, "'fields' must be a mapping of key: value pairs."));

        foreach (var (key, value) in Pairs(fields))
            list.Add(new KeyValuePair<string, string>(key, RequireScalarValue(file, value, $"field '{key}'")));
        return list;
    }

    private static IReadOnlyList<string> ParseCategories(string file, YamlMappingNode map)
    {
        var cats = new List<string>();
        if (!TryGet(map, "categories", out var node))
            return cats;
        switch (node)
        {
            case YamlSequenceNode seq:
                foreach (var c in seq.Children)
                    cats.Add(RequireScalarValue(file, c, "category"));
                break;
            case YamlScalarNode { Value: { } single }:
                cats.Add(single);
                break;
            default:
                throw new AuthoringException(At(file, node, "'categories' must be a list of strings."));
        }
        return cats;
    }

    private static IReadOnlyList<RuleDirective> ParseRules(string file, YamlMappingNode map)
    {
        var rules = new List<RuleDirective>();
        if (!TryGet(map, "rules", out var node))
            return rules;
        if (node is not YamlSequenceNode seq)
            throw new AuthoringException(At(file, node, "'rules' must be a list of directives."));

        foreach (var item in seq.Children)
        {
            if (item is not YamlMappingNode dm)
                throw new AuthoringException(At(file, item, "each rule must be a mapping (e.g. '- statadd: Strength')."));
            rules.Add(ParseDirective(file, dm));
        }
        return rules;
    }

    // -- Directive parsing ------------------------------------------------------

    private static RuleDirective ParseDirective(string file, YamlMappingNode dm)
    {
        string? disc = null;
        foreach (var (key, _) in Pairs(dm))
        {
            if (!DirectiveKeys.Contains(key))
                continue;
            if (disc is not null)
                throw new AuthoringException(At(file, dm, $"a rule must have exactly one directive key, found '{disc}' and '{key}'."));
            disc = key;
        }
        if (disc is null)
            throw new AuthoringException(At(file, dm, $"rule has no directive key (one of: {string.Join(", ", DirectiveKeys)})."));

        var discValue = ValueOf(dm, disc)!;
        // Scalar-primary directives carry their options as siblings of the
        // discriminator; mapping directives (select/replace/drop) may nest their
        // fields under the key. Either way, 'src' is where options are read from.
        var src = discValue is YamlMappingNode inner ? inner : dm;
        int? level = OptionalInt(file, src, "level");
        string? requires = OptionalScalar(src, "requires");

        return disc switch
        {
            "statadd" => new StatAddDirective
            {
                Name = ScalarPrimary(file, discValue, disc),
                Value = ParseValue(file, src),
                BonusType = OptionalScalar(src, "bonusType"),
                Condition = OptionalScalar(src, "condition"),
                Wearing = OptionalScalar(src, "wearing"),
                NotWearing = OptionalScalar(src, "notWearing"),
                Zero = OptionalBool(file, src, "zero"),
                NonZero = OptionalBool(file, src, "nonZero"),
                HalfPoint = OptionalBool(file, src, "halfPoint"),
                StatMin = OptionalScalar(src, "statMin"),
                Level = level,
                Requires = requires,
            },
            "statalias" => new StatAliasDirective
            {
                Name = ScalarPrimary(file, discValue, disc),
                Alias = RequireScalar(file, src, "alias"),
                Level = level,
                Requires = requires,
            },
            "grant" => new GrantDirective
            {
                Name = ScalarPrimary(file, discValue, disc),
                ElementType = RequireScalar(file, src, "type"),
                Level = level,
                Requires = requires,
            },
            "suggest" => new SuggestDirective
            {
                Name = ScalarPrimary(file, discValue, disc),
                ElementType = RequireScalar(file, src, "type"),
                Level = level,
                Requires = requires,
            },
            "textstring" => new TextStringDirective
            {
                Name = ScalarPrimary(file, discValue, disc),
                Value = RequireScalar(file, src, "value"),
                Condition = OptionalScalar(src, "condition"),
                Level = level,
                Requires = requires,
            },
            "modify" => new ModifyDirective
            {
                Field = ScalarPrimary(file, discValue, disc),
                Name = OptionalScalar(src, "name"),
                ElementType = OptionalScalar(src, "type"),
                Value = OptionalScalar(src, "value"),
                SelectSlot = OptionalScalar(src, "selectSlot"),
                ListAddition = OptionalScalar(src, "listAddition"),
                Wearing = OptionalScalar(src, "wearing"),
                DieIncrease = OptionalInt(file, src, "dieIncrease"),
                Level = level,
                Requires = requires,
            },
            "drop" => new DropDirective
            {
                SelectSlot = OptionalScalar(src, "selectSlot"),
                Name = (discValue is YamlScalarNode { Value: { Length: > 0 } sc }) ? sc : OptionalScalar(src, "name"),
                ElementType = OptionalScalar(src, "type"),
                Level = level,
                Requires = requires,
            },
            "select" => new SelectDirective
            {
                ElementType = RequireScalar(file, src, "type"),
                Number = OptionalInt(file, src, "number") ?? 1,
                Category = OptionalScalar(src, "category"),
                Name = OptionalScalar(src, "name"),
                DisplayLabel = OptionalScalar(src, "displayLabel"),
                Prepare = OptionalScalar(src, "prepare"),
                Spellbook = OptionalScalar(src, "spellbook"),
                Optional = OptionalBool(file, src, "optional"),
                Existing = OptionalBool(file, src, "existing"),
                Default = OptionalScalar(src, "default"),
                Grant = OptionalScalar(src, "grant"),
                Level = level,
                Requires = requires,
            },
            "replace" => new ReplaceDirective
            {
                Name = OptionalScalar(src, "name"),
                Multiclass = OptionalScalar(src, "multiclass"),
                PowerSwap = OptionalScalar(src, "powerSwap"),
                PowerReplace = OptionalScalar(src, "powerReplace"),
                Optional = OptionalBool(file, src, "optional"),
                Level = level,
                Requires = requires,
            },
            _ => throw new AuthoringException(At(file, dm, $"unknown directive '{disc}'.")),
        };
    }

    private static ValueExpression ParseValue(string file, YamlMappingNode src)
    {
        if (!TryGet(src, "value", out var node))
            throw new AuthoringException(At(file, src, "'statadd' requires a 'value'."));

        // Scalar form uses the engine's compact value grammar:
        //   2 | +2 | -1 | +Strength modifier | +ABILITYMOD(Wisdom) | +HALF-LEVEL | Shield Bonus
        if (node is YamlScalarNode { Value: { } scalar })
            return ValueExpression.Parse(scalar);

        // Explicit mapping form, for control the compact grammar can't express
        // (scale factors, absolute references, negation).
        if (node is YamlMappingNode m)
        {
            if (TryGet(m, "literal", out var lit))
                return new ValueExpression.Literal(RequireIntValue(file, lit, "literal"));
            if (TryGet(m, "statref", out var sr))
                return new ValueExpression.StatReference(
                    RequireScalarValue(file, sr, "statref"),
                    OptionalInt(file, m, "scale") ?? 1,
                    OptionalBool(file, m, "abs"));
            if (TryGet(m, "abilmod", out var am))
                return new ValueExpression.AbilityModifier(RequireScalarValue(file, am, "abilmod"));
            if (TryGet(m, "abilmodfunc", out var amf))
                return new ValueExpression.AbilityModFunction(
                    RequireScalarValue(file, amf, "abilmodfunc"),
                    OptionalBool(file, m, "negate"));
            throw new AuthoringException(At(file, m, "value mapping must use one of: literal, statref, abilmod, abilmodfunc."));
        }

        throw new AuthoringException(At(file, node, "value must be a number/string or a value-expression mapping."));
    }

    // -- Validation -------------------------------------------------------------

    private static IReadOnlyList<string> Validate(IReadOnlyList<ParsedElement> elements)
    {
        var warnings = new List<string>();

        var ids = new Dictionary<string, RulesElement>(StringComparer.Ordinal);
        var duplicates = new List<string>();
        foreach (var pe in elements)
        {
            if (!ids.TryAdd(pe.Element.InternalId, pe.Element))
                duplicates.Add(pe.Element.InternalId);
        }
        if (duplicates.Count > 0)
            throw new AuthoringException(
                $"Duplicate element id(s): {string.Join(", ", duplicates.Distinct())}. Each 'id' must be unique.");

        // Dangling references: grant/suggest targets (and select grant/default)
        // that point at an id not present in the authored set. Internal plumbing
        // ids (ID_INTERNAL_*) are commonly provided elsewhere, so only warn.
        foreach (var pe in elements)
        {
            foreach (var rule in pe.Element.Rules)
            {
                foreach (var (refId, kind) in ReferencedIds(rule))
                {
                    if (!ids.ContainsKey(refId))
                        warnings.Add($"{pe.Element.InternalId} ({pe.Element.Type}): {kind} references undefined id '{refId}'.");
                }
            }
        }
        return warnings;
    }

    private static IEnumerable<(string Id, string Kind)> ReferencedIds(RuleDirective rule)
    {
        switch (rule)
        {
            case GrantDirective g:
                yield return (g.Name, "grant");
                break;
            case SuggestDirective s:
                yield return (s.Name, "suggest");
                break;
            case SelectDirective sel:
                if (sel.Grant is { } sg) yield return (sg, "select grant");
                if (sel.Default is { } sd) yield return (sd, "select default");
                break;
        }
    }

    // -- YAML helpers -----------------------------------------------------------

    private static IEnumerable<(string Key, YamlNode Value)> Pairs(YamlMappingNode map)
    {
        foreach (var pair in map.Children)
            if (pair.Key is YamlScalarNode { Value: { } key })
                yield return (key, pair.Value);
    }

    private static YamlNode? ValueOf(YamlMappingNode map, string key)
    {
        foreach (var pair in map.Children)
            if (pair.Key is YamlScalarNode { Value: { } k } && k == key)
                return pair.Value;
        return null;
    }

    private static bool TryGet(YamlMappingNode map, string key, out YamlNode value)
    {
        value = ValueOf(map, key)!;
        return value is not null;
    }

    private static string? OptionalScalar(YamlMappingNode map, string key)
        => ValueOf(map, key) is YamlScalarNode { Value: { } v } ? v : null;

    private static string RequireScalar(string file, YamlMappingNode map, string key)
    {
        if (ValueOf(map, key) is YamlScalarNode { Value: { Length: > 0 } v })
            return v;
        throw new AuthoringException(At(file, map, $"missing required '{key}'."));
    }

    private static int? OptionalInt(string file, YamlMappingNode map, string key)
    {
        if (ValueOf(map, key) is not YamlScalarNode { Value: { } v })
            return null;
        if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            return n;
        throw new AuthoringException(At(file, map, $"'{key}' must be an integer, got '{v}'."));
    }

    private static bool OptionalBool(string file, YamlMappingNode map, string key)
    {
        if (ValueOf(map, key) is not YamlScalarNode { Value: { } v })
            return false;
        return v.ToLowerInvariant() switch
        {
            "true" or "yes" or "1" => true,
            "false" or "no" or "0" => false,
            _ => throw new AuthoringException(At(file, map, $"'{key}' must be true or false, got '{v}'.")),
        };
    }

    private static string ScalarPrimary(string file, YamlNode discValue, string disc)
    {
        if (discValue is YamlScalarNode { Value: { Length: > 0 } v })
            return v;
        throw new AuthoringException(At(file, discValue, $"'{disc}' must name a value (e.g. '{disc}: Strength')."));
    }

    private static string RequireScalarValue(string file, YamlNode node, string context)
    {
        if (node is YamlScalarNode { Value: { } v })
            return v;
        throw new AuthoringException(At(file, node, $"{context} must be a scalar value."));
    }

    private static int RequireIntValue(string file, YamlNode node, string context)
    {
        if (node is YamlScalarNode { Value: { } v }
            && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            return n;
        throw new AuthoringException(At(file, node, $"{context} must be an integer."));
    }

    private static string Name(string path) => Path.GetFileName(path);

    private static string At(string file, YamlNode node, string message)
    {
        var line = node.Start.Line;
        return line > 0 ? $"{Name(file)}:{line}: {message}" : $"{Name(file)}: {message}";
    }
}
