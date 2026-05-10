using Xunit;

namespace Tamp.GitVersion.V6.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Assembly_Loads_And_GitVersion_Is_Reachable()
    {
        Assert.NotNull(typeof(GitVersion));
    }
}
