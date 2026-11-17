-- relative paths are stored in lower case, so a file is identified ignoring case on every database, the same way it is found in the project.
-- a file renamed only by case has two rows in a database with a case-sensitive collation: the latest row is kept.
-- the 'where' compares binary: a case-insensitive collation sees 'Code/x.sql' and 'code/x.sql' as equal.

with numbered as (
	select row_number() over (
			partition by lower(relative_path)
			order by case when verified_utc > applied_utc then verified_utc else coalesce(applied_utc, verified_utc, created_utc) end desc
		) as rn
	from dblive.code
)
delete from numbered where rn > 1;

update dblive.code
set relative_path = lower(relative_path)
where relative_path collate Latin1_General_100_BIN2 <> lower(relative_path);
go

with numbered as (
	select row_number() over ( partition by lower(relative_path) order by run_utc desc ) as rn
	from dblive.unit_test_run
)
delete from numbered where rn > 1;

update dblive.unit_test_run
set relative_path = lower(relative_path)
where relative_path collate Latin1_General_100_BIN2 <> lower(relative_path);
go

with numbered as (
	select row_number() over ( partition by folder_type, lower(relative_path) order by completed_utc desc ) as rn
	from dblive.folder_items
)
delete from numbered where rn > 1;

update dblive.folder_items
set relative_path = lower(relative_path)
where relative_path collate Latin1_General_100_BIN2 <> lower(relative_path);
go

update dblive.migration
set relative_path = lower(relative_path)
where relative_path collate Latin1_General_100_BIN2 <> lower(relative_path);
go
