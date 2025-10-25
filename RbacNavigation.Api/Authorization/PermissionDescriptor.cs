namespace RbacNavigation.Api.Authorization;

public readonly record struct PermissionDescriptor(string Domain, string Area, string Action)
{
    public static PermissionDescriptor From(string domain, string area, string action)
        => new(domain, area, action);
}
