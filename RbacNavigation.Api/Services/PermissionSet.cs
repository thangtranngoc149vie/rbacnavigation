using System.Linq;
using System.Text.Json;

namespace RbacNavigation.Api.Services;

public sealed class PermissionSet
{
    private readonly Dictionary<string, Dictionary<string, HashSet<string>>> _permissions;

    private PermissionSet(Dictionary<string, Dictionary<string, HashSet<string>>> permissions)
    {
        _permissions = permissions;
    }

    public static PermissionSet FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PermissionSet(new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase));
        }

        var root = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string[]>>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (root is null)
        {
            return new PermissionSet(new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase));
        }

        var map = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (domain, areas) in root)
        {
            var areaMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            if (areas is not null)
            {
                foreach (var (area, actions) in areas)
                {
                    if (actions is null)
                    {
                        continue;
                    }

                    areaMap[area] = actions.Length == 0
                        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(actions, StringComparer.OrdinalIgnoreCase);
                }
            }

            map[domain] = areaMap;
        }

        return new PermissionSet(map);
    }

    public bool Has(string domain, string area, string action)
    {
        if (!_permissions.TryGetValue(domain, out var areas))
        {
            return false;
        }

        if (!areas.TryGetValue(area, out var actions))
        {
            return false;
        }

        return actions.Contains(action);
    }

    public bool Allows(IEnumerable<IReadOnlyList<string>>? requirements)
    {
        if (requirements is null)
        {
            return true;
        }

        var hasRequirement = false;
        foreach (var requirement in requirements)
        {
            if (requirement.Count < 3)
            {
                continue;
            }

            hasRequirement = true;
            if (Has(requirement[0], requirement[1], requirement[2]))
            {
                return true;
            }
        }

        return !hasRequirement;
    }

    public bool AllowsAny(params (string domain, string area, string action)[] requirements)
    {
        if (requirements.Length == 0)
        {
            return true;
        }

        foreach (var (domain, area, action) in requirements)
        {
            if (Has(domain, area, action))
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyDictionary<string, string[]> Flatten()
    {
        var result = new SortedDictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (domain, areas) in _permissions)
        {
            foreach (var (area, actions) in areas)
            {
                if (actions.Count == 0)
                {
                    continue;
                }

                var key = $"{domain}.{area}";
                result[key] = actions.OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }

        return result;
    }
}
