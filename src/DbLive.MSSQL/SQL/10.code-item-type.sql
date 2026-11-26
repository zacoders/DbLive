-- breaking scripts of the Code folder (*.breaking.sql) have rows in dblive.code next to the code files: item_type tells them apart.
-- the existing rows are code files.
-- re-runnable: a database whose dblive.version was lowered by hand runs it again.

if col_length('dblive.code', 'item_type') is null
	alter table dblive.code
	add item_type varchar(32) not null
		constraint df_dblive_code_item_type default ( 'code' );
go
