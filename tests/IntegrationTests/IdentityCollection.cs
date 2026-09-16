namespace CommerceOps.IntegrationTests;

/// <summary>
/// The identity tests share one container. Every test creates its own users and
/// product groups with unique identifiers, so they do not see each other.
///
/// Tests that assert on database-wide state -- the last active administrator,
/// login throttling, the migration chain -- deliberately stay outside this
/// collection and get their own container.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IdentityCollection : ICollectionFixture<IdentityFixture>
{
    public const string Name = "identity";
}
