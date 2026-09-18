using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.WaAkg.Domain;

namespace Nop.Plugin.Misc.WaAkg.Data;

/// <summary>
/// Creates the queue table on plugin installation.
/// </summary>
[NopMigration("2026/01/01 00:00:00", "Misc.WaAkg base schema", MigrationProcessType.Installation)]
public class SchemaMigration : AutoReversingMigration
{
    /// <summary>Apply the migration.</summary>
    public override void Up()
    {
        Create.TableFor<QueuedWhatsAppMessage>();

        Create.Index("IX_QueuedWhatsAppMessage_SentOnUtc")
            .OnTable(nameof(QueuedWhatsAppMessage))
            .OnColumn(nameof(QueuedWhatsAppMessage.SentOnUtc)).Ascending();

        Create.Index("IX_QueuedWhatsAppMessage_Due")
            .OnTable(nameof(QueuedWhatsAppMessage))
            .OnColumn(nameof(QueuedWhatsAppMessage.ScheduledOnUtc)).Ascending()
            .OnColumn(nameof(QueuedWhatsAppMessage.SentOnUtc)).Ascending();
    }
}
