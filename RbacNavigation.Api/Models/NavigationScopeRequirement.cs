using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RbacNavigation.Api.Models;

public sealed record NavigationScopeRequirement(string Scope)
{
    public static bool TryCreateFromSegments(IReadOnlyList<string> segments, out NavigationScopeRequirement? requirement)
    {
        requirement = null;
        if (segments.Count < 3)
        {
            return false;
        }

        var normalized = NormalizeParts(segments);
        if (normalized is null)
        {
            return false;
        }

        requirement = new NavigationScopeRequirement(normalized);
        return true;
    }

    public static bool TryCreateFromString(string? value, out NavigationScopeRequirement? requirement)
    {
        requirement = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var segments = value.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return TryCreateFromSegments(segments, out requirement);
    }

    private static string? NormalizeParts(IReadOnlyList<string> parts)
    {
        var trimmed = new List<string>(parts.Count);
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                return null;
            }

            trimmed.Add(part.Trim());
        }

        if (trimmed.Count != 3)
        {
            return null;
        }

        return string.Join(':', trimmed);
    }
}

public sealed class NavigationScopeRequirementCollectionConverter : JsonConverter<List<NavigationScopeRequirement>?>
{
    public override List<NavigationScopeRequirement>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return new List<NavigationScopeRequirement>();
        }

        var list = new List<NavigationScopeRequirement>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    if (NavigationScopeRequirement.TryCreateFromString(element.GetString(), out var stringRequirement) && stringRequirement is not null)
                    {
                        list.Add(stringRequirement);
                    }
                    break;
                case JsonValueKind.Array:
                    var segments = new List<string>();
                    foreach (var segmentElement in element.EnumerateArray())
                    {
                        if (segmentElement.ValueKind == JsonValueKind.String)
                        {
                            var segment = segmentElement.GetString();
                            if (!string.IsNullOrWhiteSpace(segment))
                            {
                                segments.Add(segment.Trim());
                            }
                        }
                    }

                    if (NavigationScopeRequirement.TryCreateFromSegments(segments, out var tupleRequirement) && tupleRequirement is not null)
                    {
                        list.Add(tupleRequirement);
                    }
                    break;
            }
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<NavigationScopeRequirement>? value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        if (value is not null)
        {
            foreach (var requirement in value)
            {
                writer.WriteStringValue(requirement.Scope);
            }
        }
        writer.WriteEndArray();
    }
}
