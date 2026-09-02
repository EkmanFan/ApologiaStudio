using ApologiaStudio.Web.DocumentManager;
using Microsoft.Extensions.Configuration;

namespace ApologiaStudio.UnitTests.Web;

public sealed class DocumentManagerAdministrationOptionsTests
{
    [Fact]
    public void Administration_is_disabled_by_default()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options =
            DocumentManagerAdministrationOptions.FromConfiguration(
                configuration);

        Assert.False(options.Enabled);
    }

    [Fact]
    public void Administration_can_be_explicitly_enabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DocumentManagerAdministration:Enabled"] = "true"
                })
            .Build();

        var options =
            DocumentManagerAdministrationOptions.FromConfiguration(
                configuration);

        Assert.True(options.Enabled);
    }
}
