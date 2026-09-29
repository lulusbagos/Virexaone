using Npgsql;
using Virexaone.FMS.Backend.Models;

namespace Virexaone.FMS.Backend.Services;

public sealed class FmsCatalogService(NpgsqlDataSource dataSource)
{
    public async Task<List<FmsMenuCatalogItem>> GetMenusAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT menu.code, parent.code AS parent_code, menu.title, menu.module_code,
                   menu.doc_section, menu.route_key, menu.display_order, menu.readiness,
                   menu.is_visible, menu.is_enabled
            FROM tbl_m_fms_menu_astha AS menu
            LEFT JOIN tbl_m_fms_menu_astha AS parent ON parent.id = menu.parent_id
            ORDER BY menu.module_code, parent.display_order NULLS FIRST,
                     menu.display_order, menu.code
            """;

        await using var command = dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var menus = new List<FmsMenuCatalogItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            menus.Add(new FmsMenuCatalogItem(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetInt32(6),
                reader.GetString(7),
                reader.GetBoolean(8),
                reader.GetBoolean(9)));
        }
        return menus;
    }
}
