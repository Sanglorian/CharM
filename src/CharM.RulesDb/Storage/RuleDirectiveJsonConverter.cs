using System.Text.Json;
using System.Text.Json.Serialization;
using CharM.Engine.Rules;

namespace CharM.RulesDb.Storage;

/// <summary>
/// Polymorphic JSON converter for <see cref="RuleDirective"/> and its 9 subtypes.
/// Uses a "$type" discriminator property to distinguish subtypes during deserialization.
/// </summary>
public sealed class RuleDirectiveJsonConverter : JsonConverter<RuleDirective>
{
    private const string Discriminator = "$type";

    public override RuleDirective? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty(Discriminator, out var typeProp))
            throw new JsonException($"Missing '{Discriminator}' discriminator in RuleDirective JSON.");

        string typeName = typeProp.GetString() ?? throw new JsonException("Null type discriminator.");

        return typeName switch
        {
            "statadd" => DeserializeStatAdd(root),
            "statalias" => DeserializeStatAlias(root),
            "grant" => DeserializeGrant(root),
            "drop" => DeserializeDrop(root),
            "select" => DeserializeSelect(root),
            "replace" => DeserializeReplace(root),
            "suggest" => DeserializeSuggest(root),
            "modify" => DeserializeModify(root),
            "textstring" => DeserializeTextString(root),
            _ => throw new JsonException($"Unknown RuleDirective type: {typeName}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, RuleDirective value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case StatAddDirective d:
                writer.WriteString(Discriminator, "statadd");
                writer.WriteString("name", d.Name);
                WriteValueExpression(writer, "value", d.Value);
                WriteOptional(writer, "bonusType", d.BonusType);
                WriteOptional(writer, "condition", d.Condition);
                WriteOptional(writer, "wearing", d.Wearing);
                WriteOptional(writer, "notWearing", d.NotWearing);
                if (d.Zero) writer.WriteBoolean("zero", true);
                if (d.NonZero) writer.WriteBoolean("nonZero", true);
                if (d.HalfPoint) writer.WriteBoolean("halfPoint", true);
                WriteOptional(writer, "statMin", d.StatMin);
                break;

            case StatAliasDirective d:
                writer.WriteString(Discriminator, "statalias");
                writer.WriteString("name", d.Name);
                writer.WriteString("alias", d.Alias);
                break;

            case GrantDirective d:
                writer.WriteString(Discriminator, "grant");
                writer.WriteString("name", d.Name);
                writer.WriteString("elementType", d.ElementType);
                break;

            case DropDirective d:
                writer.WriteString(Discriminator, "drop");
                WriteOptional(writer, "selectSlot", d.SelectSlot);
                WriteOptional(writer, "name", d.Name);
                WriteOptional(writer, "elementType", d.ElementType);
                break;

            case SelectDirective d:
                writer.WriteString(Discriminator, "select");
                writer.WriteString("elementType", d.ElementType);
                writer.WriteNumber("number", d.Number);
                WriteOptional(writer, "category", d.Category);
                WriteOptional(writer, "name", d.Name);
                WriteOptional(writer, "displayLabel", d.DisplayLabel);
                WriteOptional(writer, "prepare", d.Prepare);
                WriteOptional(writer, "spellbook", d.Spellbook);
                if (d.Optional) writer.WriteBoolean("optional", true);
                if (d.Existing) writer.WriteBoolean("existing", true);
                WriteOptional(writer, "default", d.Default);
                WriteOptional(writer, "grant", d.Grant);
                break;

            case ReplaceDirective d:
                writer.WriteString(Discriminator, "replace");
                WriteOptional(writer, "name", d.Name);
                WriteOptional(writer, "multiclass", d.Multiclass);
                WriteOptional(writer, "powerSwap", d.PowerSwap);
                WriteOptional(writer, "powerReplace", d.PowerReplace);
                if (d.Optional) writer.WriteBoolean("optional", true);
                break;

            case SuggestDirective d:
                writer.WriteString(Discriminator, "suggest");
                writer.WriteString("name", d.Name);
                writer.WriteString("elementType", d.ElementType);
                break;

            case ModifyDirective d:
                writer.WriteString(Discriminator, "modify");
                writer.WriteString("field", d.Field);
                WriteOptional(writer, "name", d.Name);
                WriteOptional(writer, "elementType", d.ElementType);
                WriteOptional(writer, "value", d.Value);
                WriteOptional(writer, "selectSlot", d.SelectSlot);
                WriteOptional(writer, "listAddition", d.ListAddition);
                WriteOptional(writer, "wearing", d.Wearing);
                if (d.DieIncrease.HasValue) writer.WriteNumber("dieIncrease", d.DieIncrease.Value);
                break;

            case TextStringDirective d:
                writer.WriteString(Discriminator, "textstring");
                writer.WriteString("name", d.Name);
                writer.WriteString("value", d.Value);
                WriteOptional(writer, "condition", d.Condition);
                break;
        }

        // Common properties
        if (value.Level.HasValue)
            writer.WriteNumber("level", value.Level.Value);
        if (value.Requires is not null)
            writer.WriteString("requires", value.Requires);

        writer.WriteEndObject();
    }

    private static void WriteOptional(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is not null)
            writer.WriteString(propertyName, value);
    }

    private static void WriteValueExpression(Utf8JsonWriter writer, string propertyName, ValueExpression expr)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartObject();
        switch (expr)
        {
            case ValueExpression.Literal lit:
                writer.WriteString("$type", "literal");
                writer.WriteNumber("val", lit.Value);
                break;
            case ValueExpression.StatReference sr:
                writer.WriteString("$type", "statref");
                writer.WriteString("stat", sr.StatName);
                writer.WriteNumber("scale", sr.ScaleFactor);
                if (sr.IsAbsolute) writer.WriteBoolean("abs", true);
                break;
            case ValueExpression.AbilityModifier am:
                writer.WriteString("$type", "abilmod");
                writer.WriteString("stat", am.StatName);
                break;
            case ValueExpression.AbilityModFunction amf:
                writer.WriteString("$type", "abilmodfunc");
                writer.WriteString("stat", amf.StatName);
                if (amf.Negate) writer.WriteBoolean("negate", true);
                break;
        }
        writer.WriteEndObject();
    }

    private static ValueExpression ReadValueExpression(JsonElement el)
    {
        string vType = el.GetProperty("$type").GetString()!;
        return vType switch
        {
            "literal" => new ValueExpression.Literal(el.GetProperty("val").GetInt32()),
            "statref" => new ValueExpression.StatReference(
                el.GetProperty("stat").GetString()!,
                el.GetProperty("scale").GetInt32(),
                el.TryGetProperty("abs", out var abs) && abs.GetBoolean()),
            "abilmod" => new ValueExpression.AbilityModifier(el.GetProperty("stat").GetString()!),
            "abilmodfunc" => new ValueExpression.AbilityModFunction(
                el.GetProperty("stat").GetString()!,
                el.TryGetProperty("negate", out var n) && n.GetBoolean()),
            _ => throw new JsonException($"Unknown ValueExpression type: {vType}"),
        };
    }

    // --- Deserialization helpers ---

    private static StatAddDirective DeserializeStatAdd(JsonElement el)
    {
        return new StatAddDirective
        {
            Name = el.GetProperty("name").GetString()!,
            Value = ReadValueExpression(el.GetProperty("value")),
            BonusType = GetOptionalString(el, "bonusType"),
            Condition = GetOptionalString(el, "condition"),
            Wearing = GetOptionalString(el, "wearing"),
            NotWearing = GetOptionalString(el, "notWearing"),
            Zero = GetOptionalBool(el, "zero"),
            NonZero = GetOptionalBool(el, "nonZero"),
            HalfPoint = GetOptionalBool(el, "halfPoint"),
            StatMin = GetOptionalString(el, "statMin"),
            Level = GetOptionalInt(el, "level"),
            Requires = GetOptionalString(el, "requires"),
        };
    }

    private static StatAliasDirective DeserializeStatAlias(JsonElement el)
    {
        return new StatAliasDirective
        {
            Name = el.GetProperty("name").GetString()!,
            Alias = el.GetProperty("alias").GetString()!,
            Level = GetOptionalInt(el, "level"),
            Requires = GetOptionalString(el, "requires"),
        };
    }

    private static GrantDirective DeserializeGrant(JsonElement el)
    {
        return new GrantDirective
        {
            Name = el.GetProperty("name").GetString()!,
            ElementType = el.GetProperty("elementType").GetString()!,
            Level = GetOptionalInt(el, "level"),
            Requires = GetOptionalString(el, "requires"),
        };
    }

    private static DropDirective DeserializeDrop(JsonElement el)
    {
        return new DropDirective
        {
            SelectSlot = GetOptionalString(el, "selectSlot"),
            Name = GetOptionalString(el, "name"),
            ElementType = GetOptionalString(el, "elementType"),
            Level = GetOptionalInt(el, "level"),
            Requires = GetOptionalString(el, "requires"),
        };
    }

    private static SelectDirective DeserializeSelect(JsonElement el)
    {
        return new SelectDirective
        {
            ElementType = el.GetProperty("elementType").GetString()!,
            Number = el.TryGetProperty("number", out var n) ? n.GetInt32() : 1,
            Category = GetOptionalString(el, "category"),
            Name = GetOptionalString(el, "name"),
            DisplayLabel = GetOptionalString(el, "displayLabel"),
            Prepare = GetOptionalString(el, "prepare"),
            Spellbook = GetOptionalString(el, "spellbook"),
            Optional = GetOptionalBool(el, "optional"),
            Existing = GetOptionalBool(el, "existing"),
            Default = GetOptionalString(el, "default"),
            Grant = GetOptionalString(el, "grant"),
            Level = GetOptionalInt(el, "level"),
            Requires = GetOptionalString(el, "requires"),
        };
    }

    private static ReplaceDirective DeserializeReplace(JsonElement el)
    {
        return new ReplaceDirective
        {
            Name = GetOptionalString(el, "name"),
            Multiclass = GetOptionalString(el, "multiclass"),
            PowerSwap = GetOptionalString(el, "powerSwap"),
            PowerReplace = GetOptionalString(el, "powerReplace"),
            Optional = GetOptionalBool(el, "optional"),
            Level = GetOptionalInt(el, "level"),
            Requires = GetOptionalString(el, "requires"),
        };
    }

    private static SuggestDirective DeserializeSuggest(JsonElement el)
    {
        return new SuggestDirective
        {
            Name = el.GetProperty("name").GetString()!,
            ElementType = el.GetProperty("elementType").GetString()!,
            Level = GetOptionalInt(el, "level"),
            Requires = GetOptionalString(el, "requires"),
        };
    }

    private static ModifyDirective DeserializeModify(JsonElement el)
    {
        return new ModifyDirective
        {
            Field = el.GetProperty("field").GetString()!,
            Name = GetOptionalString(el, "name"),
            ElementType = GetOptionalString(el, "elementType"),
            Value = GetOptionalString(el, "value"),
            SelectSlot = GetOptionalString(el, "selectSlot"),
            ListAddition = GetOptionalString(el, "listAddition"),
            Wearing = GetOptionalString(el, "wearing"),
            DieIncrease = GetOptionalInt(el, "dieIncrease"),
            Level = GetOptionalInt(el, "level"),
            Requires = GetOptionalString(el, "requires"),
        };
    }

    private static TextStringDirective DeserializeTextString(JsonElement el)
    {
        return new TextStringDirective
        {
            Name = el.GetProperty("name").GetString()!,
            Value = el.GetProperty("value").GetString()!,
            Level = GetOptionalInt(el, "level"),
            Requires = GetOptionalString(el, "requires"),
            Condition = GetOptionalString(el, "condition"),
        };
    }

    private static string? GetOptionalString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) ? v.GetString() : null;

    private static int? GetOptionalInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) ? v.GetInt32() : null;

    private static bool GetOptionalBool(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.GetBoolean();
}
