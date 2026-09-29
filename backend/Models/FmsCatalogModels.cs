using System.Text.Json.Serialization;

namespace Virexaone.FMS.Backend.Models;

public record FmsMenuCatalogItem(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("parent_code")] string? ParentCode,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("module_code")] string ModuleCode,
    [property: JsonPropertyName("document_section")] string? DocumentSection,
    [property: JsonPropertyName("route_key")] string? RouteKey,
    [property: JsonPropertyName("display_order")] int DisplayOrder,
    [property: JsonPropertyName("readiness")] string Readiness,
    [property: JsonPropertyName("is_visible")] bool IsVisible,
    [property: JsonPropertyName("is_enabled")] bool IsEnabled
);
