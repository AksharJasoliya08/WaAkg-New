using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.WaAkg.Domain;

namespace Nop.Plugin.Misc.WaAkg.Data;

/// <summary>
/// Adds the WaAkgErrorLog table for sites where the plugin was already installed before
/// this table existed. Runs once, keyed on this specific version string - if the table
/// already exists (fresh install via SchemaMigration), this is a no-op guarded by TableExists.
/// </summary>
[NopMigration("2026/09/17 00:00:00", "Misc.WaAkg add error log table", MigrationProcessType.Update)]
public class ErrorLogMigration : Migration
{
    /// <summary>Apply the migration.</summary>
    public override void Up()
    {
        if (Schema.Table(nameof(WaAkgErrorLog)).Exists())
            return;

        Create.TableFor<WaAkgErrorLog>();

        Create.Index("IX_WaAkgErrorLog_CreatedOnUtc")
            .OnTable(nameof(WaAkgErrorLog))
            .OnColumn(nameof(WaAkgErrorLog.CreatedOnUtc)).Descending();
    }

    /// <summary>No rollback needed for an additive table.</summary>
    public override void Down()
    {
    }
}
