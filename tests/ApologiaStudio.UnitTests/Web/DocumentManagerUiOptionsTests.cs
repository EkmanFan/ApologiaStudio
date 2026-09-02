using ApologiaStudio.Web.DocumentManager;
using Microsoft.Extensions.Configuration;

namespace ApologiaStudio.UnitTests.Web;

public sealed class DocumentManagerUiOptionsTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5092/")]
    [InlineData("http://localhost:5092/")]
    [InlineData("https://manager.apologia.example/")]
    public void FromConfiguration_ShouldAcceptSafeAddress(
        string value)
    {
        var configuration = CreateConfiguration(value);

        var options =
            DocumentManagerUiOptions.FromConfiguration(
                configuration);

        Assert.Equal(new Uri(value), options.Address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("manager.local")]
    [InlineData("http://manager.apologia.example/")]
    [InlineData("file:///tmp/manager.html")]
    public void FromConfiguration_ShouldRejectUnsafeAddress(
        string value)
    {
        var configuration = CreateConfiguration(value);

        Assert.Throws<InvalidOperationException>(
            () => DocumentManagerUiOptions.FromConfiguration(
                configuration));
    }

    private static IConfiguration CreateConfiguration(
        string value)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DocumentManager:UiUrl"] = value
                })
            .Build();
    }
}
