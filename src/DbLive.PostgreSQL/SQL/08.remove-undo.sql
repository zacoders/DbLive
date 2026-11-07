-- undo scripts and the database downgrade are removed.
-- the rows of undo scripts and their settings go.
-- a row with status 'reverted' belongs to a version that a downgrade rolled back, so the version is not applied: the row goes,
-- the next deployment saves it again, and the migration runs if the project has it.
-- dblive.migration.content held only the content of undo scripts and their settings.
-- re-runnable: a database whose dblive.version was lowered by hand runs it again.

delete from dblive.migration
where item_type in ('undo', 'undo.settings')
   or status = 'reverted';

alter table dblive.migration
drop column if exists content;
