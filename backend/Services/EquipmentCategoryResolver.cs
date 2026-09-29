namespace Virexaone.FMS.Backend.Services;

public sealed class EquipmentCategoryResolver
{
    private readonly Dictionary<string, string> _typeOverrides;
    private static readonly HashSet<string> SupportedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hauler", "Excavator", "Bulldozer", "Grader", "WheelLoader", "FuelTruck", "Support"
    };

    public EquipmentCategoryResolver(IConfiguration configuration)
    {
        _typeOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in configuration.GetSection("FmsSettings:UnitCategories").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(entry.Value) && SupportedCategories.Contains(entry.Value))
                _typeOverrides[entry.Key.Trim()] = entry.Value;
        }
    }

    public string Resolve(string? equipmentType, string? equipmentName)
    {
        string type = equipmentType?.Trim() ?? "";
        if (_typeOverrides.TryGetValue(type, out string? category)) return category;

        if (type.Contains("excavat", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("shovel", StringComparison.OrdinalIgnoreCase)) return "Excavator";
        if (type.Contains("dozer", StringComparison.OrdinalIgnoreCase)) return "Bulldozer";
        if (type.Contains("grader", StringComparison.OrdinalIgnoreCase)) return "Grader";
        if (type.Contains("wheel loader", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("wheel-loader", StringComparison.OrdinalIgnoreCase)) return "WheelLoader";
        if (type.Contains("fuel", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("service", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("water", StringComparison.OrdinalIgnoreCase)) return "FuelTruck";
        if (type.Contains("truck", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("hauler", StringComparison.OrdinalIgnoreCase)) return "Hauler";

        // Name prefixes are only a fallback for source systems with no useful type value.
        string name = equipmentName?.Trim() ?? "";
        if (name.StartsWith("EX", StringComparison.OrdinalIgnoreCase)) return "Excavator";
        if (name.StartsWith("DZ", StringComparison.OrdinalIgnoreCase)) return "Bulldozer";
        if (name.StartsWith("GD", StringComparison.OrdinalIgnoreCase)) return "Grader";
        if (name.StartsWith("RD", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("DT", StringComparison.OrdinalIgnoreCase)) return "Hauler";
        return "Support";
    }
}
