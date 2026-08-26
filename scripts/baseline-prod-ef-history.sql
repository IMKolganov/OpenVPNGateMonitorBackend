-- EF Core baseline for production: mark migrations as applied when their effects
-- are already in the database but __EFMigrationsHistoryDashboardVpn was never updated.
-- See https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#baseline
--
-- Run ONCE against prod BEFORE deploying backend that fixes per-migration Migrate(target)
-- (see DatabaseMigrationExtensions). Safe to re-run (ON CONFLICT DO NOTHING).

INSERT INTO xgb_dashopnvpn."__EFMigrationsHistoryDashboardVpn" ("MigrationId", "ProductVersion")
VALUES
    ('20260531120000_TrafficDailyRollupNotificationKinds', '10.0.9'),
    ('20260705120000_FreeTierGraceAccessSettings', '10.0.9'),
    ('20260705220000_FreeTierOpenVpnEnforcementSettings', '10.0.9')
ON CONFLICT ("MigrationId") DO NOTHING;
