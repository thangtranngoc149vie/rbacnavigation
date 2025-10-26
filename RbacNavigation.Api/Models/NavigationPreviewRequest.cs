using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RbacNavigation.Api.Models;

public sealed class NavigationPreviewRequest
{
    [JsonPropertyName("org_id")]
    public Guid? OrgId { get; init; }

    [JsonPropertyName("draft_nav_value")]
    public JsonElement DraftNavValue { get; init; }

    [JsonPropertyName("as_user_id")]
    public Guid? AsUserId { get; init; }

    [JsonPropertyName("as_role_id")]
    public Guid? AsRoleId { get; init; }

    [JsonPropertyName("as_permissions")]
    public List<string>? AsPermissions { get; init; }

    [JsonPropertyName("include_hidden")]
    public bool? IncludeHidden { get; init; }

    [JsonPropertyName("return_reason")]
    public bool? ReturnReason { get; init; }

    public bool HasDraftNavValue => DraftNavValue.ValueKind != JsonValueKind.Undefined && DraftNavValue.ValueKind != JsonValueKind.Null;
}
