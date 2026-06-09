using System.Xml.Linq;
using CharM.Engine.CharacterModel;
using CharM.Engine.Creation;
using CharM.Engine.Rules;
using CharM.Serialization;

namespace CharM.ImportExport;

public static partial class Dnd4eExporter
{
    private static Dictionary<int, List<XElement>> BuildGeneratedHouseruleUserEdits(
        CharacterSession session,
        CharM.Engine.Creation.CharacterSnapshot? snapshot)
    {
        var result = new Dictionary<int, List<XElement>>();
        var grantsByLevel = session.HouseruleGrants
            .Where(g => g.Kind == HouseruleGrantKind.RulesElement)
            .GroupBy(g => g.AtLevel > 0 ? g.AtLevel : session.Level)
            .OrderBy(g => g.Key);

        foreach (var levelGroup in grantsByLevel)
        {
            var grants = levelGroup.ToList();
            if (grants.Count == 0) continue;

            var wrapper = Dnd4eWriter.CreateRulesElementXml(
                "",
                "",
                charelem: Dnd4eWriter.GenerateCharelem(
                    "UserEdit:" + levelGroup.Key + ":" + string.Join(",", grants.Select(g => g.Element.InternalId))),
                legality: "houserule");

            foreach (var grant in grants)
            {
                var node = FindActiveElementNode(snapshot, grant.Element.InternalId);
                wrapper.Add(node is not null
                    ? SerializeGeneratedHouseruleNode(node, session)
                    : SerializeGeneratedHouseruleElement(grant.Element, session));
            }

            var rules = new XElement("rules");
            foreach (var grant in grants)
            {
                rules.Add(new XElement("select",
                    new XAttribute("type", grant.Element.Type),
                    new XAttribute("number", "1")));
            }

            result[levelGroup.Key] = [new XElement("UserEdit", wrapper, rules)];
        }

        return result;
    }

    private static List<XElement> BuildGeneratedHouseruleTallyMirror(CharacterSession session)
    {
        var result = new List<XElement>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var grant in session.HouseruleGrants
            .Where(g => g.Kind == HouseruleGrantKind.RulesElement))
        {
            if (string.IsNullOrWhiteSpace(grant.Element.InternalId)) continue;
            if (!seen.Add(grant.Element.InternalId)) continue;

            result.Add(Dnd4eWriter.CreateRulesElementXml(
                grant.Element.Name,
                grant.Element.Type,
                grant.Element.InternalId));
        }

        return result;
    }

    private static CharacterElement? FindActiveElementNode(
        CharM.Engine.Creation.CharacterSnapshot? snapshot,
        string internalId)
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(internalId))
            return null;

        return snapshot.Builder.ElementTree.Root.GetAllDescendants()
            .FirstOrDefault(node =>
                string.Equals(node.RulesElement?.InternalId, internalId, StringComparison.OrdinalIgnoreCase));
    }

    private static XElement SerializeGeneratedHouseruleNode(CharacterElement node, CharacterSession session)
    {
        var element = node.RulesElement;
        if (element is null)
        {
            return Dnd4eWriter.CreateRulesElementXml("", "", charelem: "deadbeef");
        }

        var xml = SerializeGeneratedHouseruleElement(element, session);
        foreach (var child in node.Children)
        {
            if (child.RulesElement is null) continue;
            xml.Add(SerializeGeneratedHouseruleNode(child, session));
        }

        return xml;
    }

    private static XElement SerializeGeneratedHouseruleElement(RulesElement element, CharacterSession session)
    {
        var xml = Dnd4eWriter.CreateRulesElementXml(
            element.Name,
            element.Type,
            string.IsNullOrWhiteSpace(element.InternalId) ? null : element.InternalId,
            legality: session.IsHouseruledElement(element.InternalId) ? "houserule" : "rules-legal");

        return xml;
    }
}
