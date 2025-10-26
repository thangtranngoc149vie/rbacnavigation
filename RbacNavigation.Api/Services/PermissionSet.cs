using System.Linq;
using System.Text.Json;
using RbacNavigation.Api.Models;

namespace RbacNavigation.Api.Services;

public sealed class PermissionSet
{
    private readonly Dictionary<string, Dictionary<string, HashSet<string>>> _permissions;
    private readonly HashSet<string> _flatScopes;

    private PermissionSet(
        Dictionary<string, Dictionary<string, HashSet<string>>> permissions,
        HashSet<string> flatScopes)
    {
        _permissions = permissions;
        _flatScopes = flatScopes;
    }

    public static PermissionSet FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PermissionSet(
                new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var root = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string[]>>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (root is null)
        {
            return new PermissionSet(
                new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var map = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
        var flat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (domain, areas) in root)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                continue;
            }

            var domainKey = domain.Trim();
            var areaMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            if (areas is not null)
            {
                foreach (var (area, actions) in areas)
                {
                    if (actions is null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(area))
                    {
                        continue;
                    }

                    var areaKey = area.Trim();
                    var actionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (actions is not null)
                    {
                        foreach (var action in actions)
                        {
                            if (!TryNormalizeAction(domainKey, areaKey, action, out var normalizedAction, out var normalizedScope))
                            {
                                continue;
                            }

                            actionSet.Add(normalizedAction);
                            flat.Add(normalizedScope);
                        }
                    }

                    areaMap[areaKey] = actionSet;
                }
            }

            map[domainKey] = areaMap;
        }

        return new PermissionSet(map, flat);
    }

    public static PermissionSet FromFlatScopes(IEnumerable<string> scopes)
    {
        var map = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
        var flat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scope in scopes)
        {
            if (!TryNormalizeScope(scope, out var domain, out var area, out var action, out var normalizedScope))
            {
                continue;
            }

            if (!map.TryGetValue(domain, out var areas))
            {
                areas = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                map[domain] = areas;
            }

            if (!areas.TryGetValue(area, out var actions))
            {
                actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                areas[area] = actions;
            }

            actions.Add(action);
            flat.Add(normalizedScope);
        }

        return new PermissionSet(map, flat);
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

    public bool Allows(IEnumerable<NavigationScopeRequirement>? requirements)
    {
        return Allows(requirements, out _);
    }

    public bool Allows(IEnumerable<NavigationScopeRequirement>? requirements, out string? matchedScope)
    {
        matchedScope = null;
        if (requirements is null)
        {
            return true;
        }

        var hasRequirement = false;
        foreach (var requirement in requirements)
        {
            if (requirement is null)
            {
                continue;
            }

            hasRequirement = true;
            if (TryMatchScope(requirement.Scope, out matchedScope))
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

    private static bool TryNormalizeAction(
        string domain,
        string area,
        string action,
        out string normalizedAction,
        out string normalizedScope)
    {
        normalizedAction = string.Empty;
        normalizedScope = string.Empty;
        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(area) || string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        normalizedAction = action.Trim();
        if (normalizedAction.Length == 0)
        {
            return false;
        }

        normalizedScope = string.Join(':', domain.Trim(), area.Trim(), normalizedAction);
        return true;
    }

    private static bool TryNormalizeScope(
        string scope,
        out string domain,
        out string area,
        out string action,
        out string normalizedScope)
    {
        domain = string.Empty;
        area = string.Empty;
        action = string.Empty;
        normalizedScope = string.Empty;

        if (string.IsNullOrWhiteSpace(scope))
        {
            return false;
        }

        var parts = scope.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        domain = parts[0];
        area = parts[1];
        action = parts[2];

        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(area) || string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        normalizedScope = string.Join(':', domain.Trim(), area.Trim(), action.Trim());
        return true;
    }

    private bool TryMatchScope(string scope, out string? matchedScope)
    {
        matchedScope = null;
        if (string.IsNullOrWhiteSpace(scope))
        {
            return false;
        }

        if (_flatScopes.Contains(scope))
        {
            matchedScope = scope;
            return true;
        }

        var wildcard = ToWildcard(scope);
        if (wildcard is not null && _flatScopes.Contains(wildcard))
        {
            matchedScope = wildcard;
            return true;
        }

        return false;
    }

    private static string? ToWildcard(string scope)
    {
        var lastSeparator = scope.LastIndexOf(':');
        if (lastSeparator <= 0)
        {
            return null;
        }

        return string.Concat(scope.AsSpan(0, lastSeparator), ":*");
    }
}
