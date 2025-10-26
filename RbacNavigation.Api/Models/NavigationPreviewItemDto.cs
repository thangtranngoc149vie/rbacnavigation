using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RbacNavigation.Api.Models;

public sealed class NavigationPreviewItemDto
{
    public NavigationPreviewItemDto(
        string key,
        string label,
        string route,
        string? icon,
        bool visible,
        IReadOnlyList<string> requires,
        string? reason,
        string? matchedScope)
    {
        Key = key;
        Label = label;
        Route = route;
        Icon = icon;
        Visible = visible;
        Requires = requires;
        Reason = reason;
        MatchedScope = matchedScope;
    }

    [JsonPropertyName("key")]
    public string Key { get; }

    [JsonPropertyName("label")]
    public string Label { get; }

    [JsonPropertyName("route")]
    public string Route { get; }

    [JsonPropertyName("icon")]
    public string? Icon { get; }

    [JsonPropertyName("visible")]
    public bool Visible { get; }

    [JsonPropertyName("requires")]
    public IReadOnlyList<string> Requires { get; }

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; }

    [JsonPropertyName("matched_scope")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MatchedScope { get; }
}
