-- relative paths are stored with '/' on every OS; DbLive on Windows used to store them with '\'.
-- a database deployed from both Windows and Linux has two rows for one file: the latest row is kept.

with numbered as (
	select row_number() over (
			partition by replace(relative_path, '\', '/')
			order by case when verified_utc > applied_utc then verified_utc else coalesce(applied_utc, verified_utc, created_utc) end desc
		) as rn
	from dblive.code
)
delete from numbered where rn > 1;

update dblive.code
set relative_path = replace(relative_path, '\', '/')
where charindex('\', relative_path) > 0;
go

with numbered as (
	select row_number() over ( partition by replace(relative_path, '\', '/') order by run_utc desc ) as rn
	from dblive.unit_test_run
)
delete from numbered where rn > 1;

update dblive.unit_test_run
set relative_path = replace(relative_path, '\', '/')
where charindex('\', relative_path) > 0;
go

with numbered as (
	select row_number() over ( partition by folder_type, replace(relative_path, '\', '/') order by completed_utc desc ) as rn
	from dblive.folder_items
)
delete from numbered where rn > 1;

update dblive.folder_items
set relative_path = replace(relative_path, '\', '/')
where charindex('\', relative_path) > 0;
go

update dblive.migration
set relative_path = replace(relative_path, '\', '/')
where charindex('\', relative_path) > 0;
go
