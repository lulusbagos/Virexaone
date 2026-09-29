namespace Virexaone.FMS.Backend.Services;

public sealed class UnitDimensionResolver
{
    private readonly Dictionary<string, double> _unitLengths = new(StringComparer.OrdinalIgnoreCase);

    public UnitDimensionResolver(IConfiguration configuration)
    {
        foreach (var entry in configuration.GetSection("FmsSettings:UnitLengthOverridesM").GetChildren())
        {
            if (double.TryParse(entry.Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double length) &&
                double.IsFinite(length) && length >= 2 && length <= 40)
                _unitLengths[entry.Key.Trim()] = length;
        }
    }

    public double Resolve(string unitName, string unitType, string category)
    {
        if (_unitLengths.TryGetValue(unitName.Trim(), out double length)) return length;
        if (unitType.Contains("LightVehicle", StringComparison.OrdinalIgnoreCase)) return 4.5;

        return category switch
        {
            "Hauler" => 10.0,
            "Excavator" => 17.0,
            "Bulldozer" => 9.2,
            "Grader" => 11.5,
            "FuelTruck" => 9.2,
            "WheelLoader" => 12.5,
            _ => 4.0
        };
    }
}
