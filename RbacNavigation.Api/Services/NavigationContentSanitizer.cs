using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RbacNavigation.Api.Services;

public sealed class NavigationContentSanitizer
{
    private static readonly JsonSerializerOptions SerializationOptions = new()
    {
        WriteIndented = false
    };

    private static readonly Regex DisallowedRouteScheme = new(
        "^\\s*javascript:\\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HtmlEncoder _htmlEncoder;

    public NavigationContentSanitizer(HtmlEncoder htmlEncoder)
    {
        _htmlEncoder = htmlEncoder;
    }

    public string Sanitize(JsonElement root)
    {
        return Sanitize(root.GetRawText());
    }

    public string Sanitize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return json;
        }

        if (node is not JsonObject obj)
        {
            return json;
        }

        SanitizeDocument(obj);
        return obj.ToJsonString(SerializationOptions);
    }

    private void SanitizeDocument(JsonObject root)
    {
        if (!root.TryGetPropertyValue("items", out var itemsNode))
        {
            return;
        }

        if (itemsNode is JsonArray items)
        {
            SanitizeItems(items);
        }
    }

    private void SanitizeItems(JsonArray items)
    {
        foreach (var element in items)
        {
            if (element is not JsonObject item)
            {
                continue;
            }

            SanitizeItem(item);

            if (item.TryGetPropertyValue("children", out var childrenNode) && childrenNode is JsonArray children)
            {
                SanitizeItems(children);
            }
        }
    }

    private void SanitizeItem(JsonObject item)
    {
        EncodeIfString(item, "label");
        EncodeIfString(item, "tooltip");
        NormalizeRoute(item);
    }

    private void EncodeIfString(JsonObject item, string propertyName)
    {
        if (!item.TryGetPropertyValue(propertyName, out var node))
        {
            return;
        }

        if (node is not JsonValue value)
        {
            return;
        }

        if (!value.TryGetValue<string>(out var raw) || string.IsNullOrEmpty(raw))
        {
            return;
        }

        item[propertyName] = _htmlEncoder.Encode(raw);
    }

    private void NormalizeRoute(JsonObject item)
    {
        if (!item.TryGetPropertyValue("route", out var node) || node is not JsonValue value)
        {
            return;
        }

        if (!value.TryGetValue<string>(out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var candidate = raw.Trim();
        if (DisallowedRouteScheme.IsMatch(candidate))
        {
            item["route"] = "#";
            return;
        }

        if (Uri.TryCreate(candidate, UriKind.RelativeOrAbsolute, out var uri))
        {
            if (uri.IsAbsoluteUri)
            {
                if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                    || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                {
                    item["route"] = uri.ToString();
                }
                else
                {
                    item["route"] = "#";
                }
            }
            else
            {
                item["route"] = candidate;
            }
        }
        else
        {
            item["route"] = "#";
        }
    }
}
