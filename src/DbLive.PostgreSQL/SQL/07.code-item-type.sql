-- breaking scripts of the Code folder (*.breaking.sql) have rows in dblive.code next to the code files: item_type tells them apart.
-- the existing rows are code files.
-- re-runnable: a database whose dblive.version was lowered by hand runs it again.

alter table dblive.code
add column if not exists item_type varchar(32) not null default 'code';
