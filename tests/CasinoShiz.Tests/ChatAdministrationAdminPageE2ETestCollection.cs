namespace CasinoShiz.Tests;

using Xunit;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ChatAdministrationAdminPageE2ETestCollection : ICollectionFixture<ChatAdministrationAdminPageE2EFixture>
{
    public const string Name = "ChatAdministration admin page E2E";
}
