using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RbacNavigation.Api.Models;

namespace RbacNavigation.Api.Services;

public sealed class NavigationComposer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public NavMapDocument Deserialize(string navJson)
    {
        return JsonSerializer.Deserialize<NavMapDocument>(navJson, SerializerOptions)
            ?? throw new InvalidOperationException("Navigation map is not a valid JSON document.");
    }

    public NavigationComposition Compose(string navJson, PermissionSet permissionSet)
    {
        var navMap = Deserialize(navJson);

        var items = new List<NavigationItemDto>();
        foreach (var item in navMap.Items)
        {
            if (permissionSet.Allows(item.Requires))
            {
                items.Add(new NavigationItemDto(item.Key, item.Label, item.Route, item.Icon));
            }
        }

        return new NavigationComposition(items, permissionSet.Flatten());
    }

    public string ComputeEtag(string? permissionsJson, string navJson)
    {
        var material = string.Concat(permissionsJson ?? string.Empty, "\n", navJson);
        var bytes = Encoding.UTF8.GetBytes(material);
        var hash = SHA256.HashData(bytes);
        return $"\"nav-{Convert.ToBase64String(hash)}\"";
    }
}

public sealed record NavigationComposition(IReadOnlyList<NavigationItemDto> Items, IReadOnlyDictionary<string, string[]> DerivedPermissions);
