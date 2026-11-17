-- the oldest DbLive version that can deploy the database, as major.minor.patch: a newer DbLive raises it when it changes
-- the dblive tables in a way older versions cannot read, and an older DbLive then stops before it changes anything.
-- null: no requirement. The self-deployer writes it after this script, in the same transaction.
-- re-runnable: a database whose dblive.version was lowered by hand runs it again.

alter table dblive.version
add column if not exists min_dblive_lib_version varchar(32) null;
