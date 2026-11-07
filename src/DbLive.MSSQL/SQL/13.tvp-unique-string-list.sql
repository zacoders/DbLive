-- a list of unique strings for a table-valued parameter: the relative paths of the project files in one parameter, whatever
-- their number (a command takes at most 2100 parameters).
-- nvarchar(450): a clustered key holds at most 900 bytes, so dblive.code never stores a longer path.
-- an older DbLive does not use the type.
-- re-runnable: a database whose dblive.version was lowered by hand runs it again.

if type_id('dblive.tvp_unique_string_list') is null
	create type dblive.tvp_unique_string_list as table ( value nvarchar(450) not null primary key );
go
