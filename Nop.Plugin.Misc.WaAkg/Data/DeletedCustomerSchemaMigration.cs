using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.WaAkg.Domain;

namespace Nop.Plugin.Misc.WaAkg.Data;

/// <summary>
/// Creates the WaAkgDeletedCustomer table for tracking soft-deleted OTP customers.
/// </summary>
[NopMigration("2026/01/02 00:00:00", "Misc.WaAkg deleted customer table", MigrationProcessType.Installation)]
public class DeletedCustomerSchemaMigration : AutoReversingMigration
{
    /// <summary>Apply the migration.</summary>
    public override void Up()
    {
        Create.TableFor<WaAkgDeletedCustomer>();

        Create.Index("IX_WaAkgDeletedCustomer_CustomerId")
            .OnTable(nameof(WaAkgDeletedCustomer))
            .OnColumn(nameof(WaAkgDeletedCustomer.CustomerId)).Ascending();

        Create.Index("IX_WaAkgDeletedCustomer_Phone")
            .OnTable(nameof(WaAkgDeletedCustomer))
            .OnColumn(nameof(WaAkgDeletedCustomer.Phone)).Ascending();

        Create.Index("IX_WaAkgDeletedCustomer_DeletedOnUtc")
            .OnTable(nameof(WaAkgDeletedCustomer))
            .OnColumn(nameof(WaAkgDeletedCustomer.DeletedOnUtc)).Descending();
    }
}
