using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace BotFramework.Host.Configuration.RuntimeTuning;

public static class RuntimeTuningMerge
{
    private static readonly JsonSerializerOptions StrictJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static T MergeSection<T>(IConfiguration configuration, string sectionPath, JsonNode? patchFragment)
        where T : class
    {
        var baseObj = configuration.GetSection(sectionPath).Get<T>() ?? Activator.CreateInstance<T>();
        if (patchFragment is not JsonObject patchObj || patchObj.Count == 0)
            return baseObj;

        var baseNode = JsonSerializer.SerializeToNode(baseObj) as JsonObject ?? new JsonObject();
        var merged = JsonObjectMerge.Merge(baseNode, patchObj);
        return merged.Deserialize<T>() ?? baseObj;
    }

    public static T MergeSectionStrict<T>(IConfiguration configuration, string sectionPath, JsonNode? patchFragment)
        where T : class
    {
        var baseObj = configuration.GetSection(sectionPath).Get<T>() ?? Activator.CreateInstance<T>();
        if (patchFragment is not JsonObject patchObj || patchObj.Count == 0)
            return baseObj;

        var baseNode = JsonSerializer.SerializeToNode(baseObj, StrictJsonOptions) as JsonObject ?? new JsonObject();
        var merged = JsonObjectMerge.Merge(baseNode, patchObj);
        return merged.Deserialize<T>(StrictJsonOptions) ?? baseObj;
    }
}
