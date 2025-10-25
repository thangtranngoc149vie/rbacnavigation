namespace RbacNavigation.Api.Authorization;

public readonly record struct OrgResource(Guid OrgId) : IOrganizationResource;
