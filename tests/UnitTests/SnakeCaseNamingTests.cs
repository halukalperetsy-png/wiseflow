using CommerceOps.Api.Infrastructure.Persistence;

namespace CommerceOps.UnitTests;

/// <summary>
/// The rewriter that replaces the EFCore.NamingConventions plugin. It runs
/// inside OnModelCreating precisely so that it cannot reach EF's own
/// __EFMigrationsHistory model; these cover the string rule itself.
/// </summary>
public sealed class SnakeCaseNamingTests
{
    [Theory]
    [InlineData("DisplayName", "display_name")]
    [InlineData("Id", "id")]
    [InlineData("NormalizedEmail", "normalized_email")]
    [InlineData("MustChangePassword", "must_change_password")]
    [InlineData("PK_users", "pk_users")]
    [InlineData("IX_user_roles_RoleId", "ix_user_roles_role_id")]
    [InlineData("AspNetUsers", "asp_net_users")]
    [InlineData("HTTPStatus", "http_status")]
    [InlineData("already_snake", "already_snake")]
    [InlineData("A", "a")]
    public void Names_are_rewritten_to_snake_case(string input, string expected) =>
        Assert.Equal(expected, SnakeCaseNaming.ToSnakeCase(input));

    [Fact]
    public void An_existing_underscore_is_never_doubled() =>
        Assert.Equal("pk___ef_migrations_history", SnakeCaseNaming.ToSnakeCase("PK___EFMigrationsHistory"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_input_is_returned_unchanged(string? input) =>
        Assert.Equal(input, SnakeCaseNaming.ToSnakeCase(input));
}
