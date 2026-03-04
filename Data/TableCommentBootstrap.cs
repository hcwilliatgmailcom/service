using Oracle.ManagedDataAccess.Client;
using System.Text.RegularExpressions;

namespace service.Data;

/// <summary>
/// Reads icon and description for each entity from the Oracle table comment.
/// Comment format:  "Description text [bi-icon-name]"
///
/// On first run, sets a default comment on tables that have none.
/// Users can change icon/description by updating the Oracle table comment:
///   COMMENT ON TABLE CMDB.DEPARTMENTS IS 'Org departments and cost centers [bi-building]';
/// </summary>
public static class TableCommentBootstrap
{
    // Default comment (description + icon) keyed by DbSet name
    private static readonly Dictionary<string, string> _defaults =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["Locations"]           = "Data centers, offices, and facility locations [bi-geo-alt]",
        ["Departments"]         = "Organizational departments and cost centers [bi-building]",
        ["Vendors"]             = "Hardware and software vendors [bi-shop]",
        ["Environments"]        = "Deployment environments (Dev, Staging, Prod) [bi-layers]",
        ["Racks"]               = "Server rack locations and capacity [bi-grid-3x3]",
        ["OperatingSystems"]    = "OS versions and lifecycle tracking [bi-cpu]",
        ["Servers"]             = "Physical and virtual server inventory [bi-hdd-rack]",
        ["Networks"]            = "Network segments, subnets, and VLANs [bi-diagram-3]",
        ["Applications"]        = "Business applications and their tiers [bi-app-indicator]",
        ["DatabaseInstances"]   = "Database engines, versions, and sizes [bi-database]",
        ["Storages"]            = "Storage systems and capacity usage [bi-device-hdd]",
        ["Firewalls"]           = "Firewall appliances and firmware [bi-shield-lock]",
        ["Switches"]            = "Network switches and port counts [bi-ethernet]",
        ["Certificates"]        = "SSL/TLS certificates and expiry dates [bi-file-earmark-lock]",
        ["Contacts"]            = "IT staff and department contacts [bi-person]",
        ["Licenses"]            = "Software licenses and seat counts [bi-key]",
        ["ServerApplications"]  = "Application installations on servers [bi-box-arrow-in-right]",
        ["ApplicationDatabases"]= "Application to database mappings [bi-link-45deg]",
        ["ServerNetworks"]      = "Server network interface assignments [bi-plug]",
        ["ContactDepartments"]  = "Contact to department associations [bi-people]",
        ["Countries"]           = "Country data synced from REST Countries API [bi-globe]",
        ["Publicholidays"]      = "Public holidays synced from Nager.Date API [bi-calendar-event]",
    };

    /// <summary>
    /// Ensures all tables have a comment (seeds defaults where missing),
    /// then returns parsed icon and description keyed by DbSet name.
    /// </summary>
    public static Dictionary<string, (string Icon, string Description)> EnsureAndLoad(
        string connectionString,
        IEnumerable<(string Schema, string TableName, string DbSetName)> entities)
    {
        var entityList = entities.ToList();

        using var conn = new OracleConnection(connectionString);
        conn.Open();

        // Load all existing table comments for the schemas in use
        var schemas = entityList.Select(e => e.Schema).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var comments = LoadComments(conn, schemas);

        // Seed defaults for tables with no comment
        foreach (var (schema, tableName, dbSetName) in entityList)
        {
            var key = $"{schema.ToUpperInvariant()}.{tableName.ToUpperInvariant()}";
            if (!comments.TryGetValue(key, out var existing) || string.IsNullOrWhiteSpace(existing))
            {
                if (_defaults.TryGetValue(dbSetName, out var defaultComment))
                {
                    SetComment(conn, schema, tableName, defaultComment);
                    comments[key] = defaultComment;
                }
            }
        }

        // Parse comments → (Icon, Description) keyed by DbSet name
        var result = new Dictionary<string, (string Icon, string Description)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (schema, tableName, dbSetName) in entityList)
        {
            var key = $"{schema.ToUpperInvariant()}.{tableName.ToUpperInvariant()}";
            if (comments.TryGetValue(key, out var comment) && !string.IsNullOrWhiteSpace(comment))
                result[dbSetName] = ParseComment(comment);
        }

        return result;
    }

    private static Dictionary<string, string> LoadComments(
        OracleConnection conn, IEnumerable<string> schemas)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var schemaList = string.Join(",", schemas.Select(s => $"'{s.ToUpperInvariant()}'"));

        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT OWNER, TABLE_NAME, COMMENTS FROM ALL_TAB_COMMENTS " +
            $"WHERE OWNER IN ({schemaList})";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var owner   = reader.GetString(0);
            var table   = reader.GetString(1);
            var comment = reader.IsDBNull(2) ? "" : reader.GetString(2);
            result[$"{owner}.{table}"] = comment;
        }

        return result;
    }

    private static void SetComment(OracleConnection conn, string schema, string tableName, string comment)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"COMMENT ON TABLE {schema}.{tableName} IS '{comment.Replace("'", "''")}'";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Parses "Some description text [bi-icon-name]" into its parts.
    /// If no bracket tag is found, returns ("bi-table", fullText).
    /// </summary>
    private static (string Icon, string Description) ParseComment(string comment)
    {
        var match = Regex.Match(comment.Trim(), @"\[([^\]]+)\]\s*$");
        if (match.Success)
        {
            var icon = match.Groups[1].Value.Trim();
            var desc = comment[..match.Index].Trim();
            return (icon, desc);
        }
        return ("bi-table", comment.Trim());
    }
}
