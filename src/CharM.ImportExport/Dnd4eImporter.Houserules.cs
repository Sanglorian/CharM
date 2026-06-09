using CharM.Engine.Creation;
using CharM.RulesDb.Storage;
using CharacterSnapshot = CharM.Serialization.CharacterSnapshot;

namespace CharM.ImportExport;

public static partial class Dnd4eImporter
{
    /// <summary>
    /// Process Form A houserule UserEdit picks captured in
    /// <c>snapshot.Houserules.LevelUserEdits</c>.
    /// </summary>
    private static void ApplyUserEditPicks(
        CharacterSession session,
        CharacterSnapshot snapshot,
        IRulesDatabase database,
        List<string> unresolved)
    {
        if (snapshot.Houserules.LevelUserEdits.Count == 0) return;

        foreach (var (level, ueList) in snapshot.Houserules.LevelUserEdits
            .OrderBy(kv => kv.Key))
        {
            foreach (var ue in ueList)
            {
                foreach (var inner in ue.Descendants("RulesElement"))
                {
                    var iid = inner.Attribute("internal-id")?.Value;
                    if (string.IsNullOrEmpty(iid)) continue;

                    var name = inner.Attribute("name")?.Value ?? "";
                    var type = inner.Attribute("type")?.Value ?? "";
                    if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(type))
                        continue;

                    var elem = database.FindByInternalId(iid)
                        ?? database.FindByNameAndType(name, type);
                    if (elem is null)
                    {
                        unresolved.Add(
                            $"UserEdit pick (not in DB): {type}::{name} (id={iid}) at level {level}");
                        continue;
                    }

                    session.AddUserEditPick(elem, atLevel: level, FindNearestUserEditOwnerId(inner, ue));
                }
            }
        }
    }

    private static string? FindNearestUserEditOwnerId(
        System.Xml.Linq.XElement inner,
        System.Xml.Linq.XElement userEdit)
    {
        for (var parent = inner.Parent; parent is not null && parent != userEdit; parent = parent.Parent)
        {
            if (parent.Name.LocalName != "RulesElement")
                continue;

            string? parentId = parent.Attribute("internal-id")?.Value;
            if (string.IsNullOrWhiteSpace(parentId))
                continue;

            string parentName = parent.Attribute("name")?.Value ?? "";
            string parentType = parent.Attribute("type")?.Value ?? "";
            if (!string.IsNullOrWhiteSpace(parentName) || !string.IsNullOrWhiteSpace(parentType))
                return parentId;
        }

        return null;
    }
}
