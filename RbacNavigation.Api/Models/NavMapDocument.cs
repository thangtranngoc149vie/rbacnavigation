using System.Text.Json.Serialization;

namespace RbacNavigation.Api.Models;

public sealed class NavMapDocument
{
    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("items")]
    public List<NavMapItem> Items { get; init; } = new();
}

public sealed class NavMapItem
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("route")]
    public string Route { get; init; } = string.Empty;

    [JsonPropertyName("requires")]
    [JsonConverter(typeof(NavigationScopeRequirementCollectionConverter))]
    public List<NavigationScopeRequirement>? Requires { get; init; }
}
