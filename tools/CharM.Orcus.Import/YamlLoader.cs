using YamlDotNet.Serialization;

namespace CharM.Orcus.Import;

public sealed class YamlElement
{
    public required string File;
    public string? Id;
    public string? Name;
    public string? Type;
    public List<string> Categories = new();
    public Dictionary<string, string> Fields = new();
}

public static class YamlLoader
{
    static readonly IDeserializer De = new DeserializerBuilder().Build();

    public static List<YamlElement> LoadDir(string dir)
    {
        var result = new List<YamlElement>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.yaml", SearchOption.AllDirectories)
                     .OrderBy(p => p))
        {
            object? doc;
            try { doc = De.Deserialize<object>(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ! could not parse {path}: {ex.Message}");
                continue;
            }
            if (doc is not IEnumerable<object> seq) continue;
            string rel = Path.GetRelativePath(dir, path);
            foreach (var item in seq)
            {
                if (item is not IDictionary<object, object> map) continue;
                var el = new YamlElement { File = rel };
                foreach (var (k, v) in map)
                {
                    switch (k?.ToString())
                    {
                        case "id": el.Id = v?.ToString(); break;
                        case "name": el.Name = v?.ToString(); break;
                        case "type": el.Type = v?.ToString(); break;
                        case "categories":
                            if (v is IEnumerable<object> cats)
                                el.Categories = cats.Select(c => c?.ToString() ?? "").ToList();
                            break;
                        case "fields":
                            if (v is IDictionary<object, object> fmap)
                                foreach (var (fk, fv) in fmap)
                                {
                                    var key = fk?.ToString();
                                    if (key != null && fv is not null && fv is not IEnumerable<object>)
                                        el.Fields[key] = fv.ToString() ?? "";
                                }
                            break;
                        // rules / other structural keys ignored
                    }
                }
                result.Add(el);
            }
        }
        return result;
    }
}
