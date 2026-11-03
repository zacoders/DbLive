-- Reverts the migration of the same version when an older build is deployed with AllowDatabaseDowngrade = true.
-- The deployment test checks that it restores the schema.
drop table if exists users;
