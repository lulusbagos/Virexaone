using Microsoft.Extensions.Configuration;
using System.Net;
using Xunit;
using Virexaone.FMS.Backend.Services;
using Virexaone.FMS.Backend.Utils;

namespace Virexaone.FMS.Backend.Tests;

public sealed class SiteConfigurationTests
{
    [Fact]
    public void ExplicitTypeMappingWinsOverLegacyNamePrefix()
    {
        var configuration = Settings(new Dictionary<string, string?>
        {
            ["FmsSettings:UnitCategories:MineLoader"] = "WheelLoader"
        });
        var resolver = new EquipmentCategoryResolver(configuration);

        Assert.Equal("WheelLoader", resolver.Resolve("MineLoader", "RD9999"));
        Assert.Equal("Excavator", resolver.Resolve("Hydraulic Excavator", "X1"));
        Assert.Equal("Support", resolver.Resolve("Unknown Machine", "U1"));
    }

    [Theory]
    [InlineData("Shovel", "EX4005", "Excavator")]
    [InlineData("Dozer", "DZ3001", "Bulldozer")]
    [InlineData("Grader", "GD4001", "Grader")]
    [InlineData(null, "DZ3001", "Bulldozer")]
    [InlineData(null, "GD4001", "Grader")]
    public void ResolvesEarthmovingUnitsFromTypeOrName(string? type, string name, string expected)
    {
        var resolver = new EquipmentCategoryResolver(Settings(new Dictionary<string, string?>()));
        Assert.Equal(expected, resolver.Resolve(type, name));
    }

    [Fact]
    public void PhysicalLengthsUseTypeDefaultsUnlessUnitIsExplicitlyMeasured()
    {
        var resolver = new UnitDimensionResolver(Settings(new Dictionary<string, string?>
        {
            ["FmsSettings:UnitLengthOverridesM:DZ3001"] = "8.7",
            ["FmsSettings:UnitLengthOverridesM:BAD"] = "80"
        }));

        Assert.Equal(8.7, resolver.Resolve("DZ3001", "Dozer", "Bulldozer"));
        Assert.Equal(9.2, resolver.Resolve("DZ7006", "Dozer", "Bulldozer"));
        Assert.Equal(11.5, resolver.Resolve("GD4001", "Grader", "Grader"));
        Assert.Equal(17.0, resolver.Resolve("EX4005", "Shovel", "Excavator"));
        Assert.Equal(10.0, resolver.Resolve("RD5001", "Truck", "Hauler"));
        Assert.Equal(4.5, resolver.Resolve("LV001", "LightVehicle", "Support"));
        Assert.Equal(4.0, resolver.Resolve("BAD", "Unknown", "Support"));
    }

    [Fact]
    public void SiteSettingsChangeProjectionAndWeatherBoundary()
    {
        var configuration = Settings(new Dictionary<string, string?>
        {
            ["FmsSettings:UtmEpsg"] = "32750",
            ["FmsSettings:SiteLat"] = "-1",
            ["FmsSettings:SiteLon"] = "117",
            ["FmsSettings:RefEasting"] = "500000",
            ["FmsSettings:RefNorthing"] = "9889469"
        });
        GeoTransform.Configure(configuration.GetSection("FmsSettings"));

        var (easting, northing) = GeoTransform.Wgs84ToUtm(117, -1);
        Assert.InRange(easting, 499999, 500001);
        Assert.InRange(northing, 9889000, 9890000);
        Assert.True(GeoTransform.IsWithinSiteRadius(-1, 117, 10));
        Assert.False(GeoTransform.IsWithinSiteRadius(1.022302, 117.657014, 10));
    }

    [Fact]
    public void LocalProxyAllowsPrivateAndExplicitClientsOnly()
    {
        string[] extra = ["203.0.113.7"];
        Assert.True(NetworkClientPolicy.IsTrusted(IPAddress.Loopback, extra));
        Assert.True(NetworkClientPolicy.IsTrusted(IPAddress.Parse("::ffff:127.0.0.1"), extra));
        Assert.True(NetworkClientPolicy.IsTrusted(IPAddress.Parse("172.16.1.10"), extra));
        Assert.True(NetworkClientPolicy.IsTrusted(IPAddress.Parse("::ffff:192.168.1.10"), extra));
        Assert.True(NetworkClientPolicy.IsTrusted(IPAddress.Parse("203.0.113.7"), extra));
        Assert.False(NetworkClientPolicy.IsTrusted(IPAddress.Parse("203.0.113.8"), extra));
        Assert.False(NetworkClientPolicy.IsTrusted(IPAddress.Parse("8.8.8.8"), extra));
        Assert.False(NetworkClientPolicy.IsTrusted(null, extra));
    }

    [Fact]
    public void RejectsUnsupportedProjection()
    {
        var configuration = Settings(new Dictionary<string, string?>
        {
            ["FmsSettings:UtmEpsg"] = "4326"
        });

        Assert.Throws<InvalidOperationException>(() =>
            GeoTransform.Configure(configuration.GetSection("FmsSettings")));
    }

    private static IConfiguration Settings(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
