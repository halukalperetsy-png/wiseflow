namespace CommerceOps.Api.Modules.Identity.Users;

internal sealed record UserListItemResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

internal sealed record UserDetailResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> ProductGroupIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record CreateUserRequest(
    string? Email,
    string? DisplayName,
    string[]? Roles,
    Guid[]? ProductGroupIds);

internal sealed record UpdateUserRequest(string? DisplayName, bool? IsActive);

internal sealed record SetRolesRequest(string[]? Roles);

internal sealed record SetProductGroupsRequest(Guid[]? ProductGroupIds);

/// <summary>
/// The only time a generated password is ever readable. It is not stored in
/// plain text, not logged, not in any URL, and no GET can return it again.
/// </summary>
internal sealed record TemporaryPasswordResponse(Guid Id, string TemporaryPassword);

internal sealed record RoleResponse(Guid Id, string Code, string DisplayName);
