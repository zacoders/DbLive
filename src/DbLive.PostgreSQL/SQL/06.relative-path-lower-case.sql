-- relative paths are stored in lower case, so a file is identified ignoring case on every database, the same way it is found in the project.
-- a file renamed only by case has two rows: the latest row is kept.

delete from dblive.code
where ctid in (
    select ctid
    from (
        select ctid,
               row_number() over (
                   partition by lower(relative_path)
                   order by greatest(applied_utc, verified_utc, created_utc) desc
               ) as rn
        from dblive.code
    ) r
    where rn > 1
);

update dblive.code
set relative_path = lower(relative_path)
where relative_path <> lower(relative_path);

delete from dblive.unit_test_run
where ctid in (
    select ctid
    from (
        select ctid, row_number() over (partition by lower(relative_path) order by run_utc desc) as rn
        from dblive.unit_test_run
    ) r
    where rn > 1
);

update dblive.unit_test_run
set relative_path = lower(relative_path)
where relative_path <> lower(relative_path);

delete from dblive.folder_items
where ctid in (
    select ctid
    from (
        select ctid, row_number() over (partition by folder_type, lower(relative_path) order by completed_utc desc) as rn
        from dblive.folder_items
    ) r
    where rn > 1
);

update dblive.folder_items
set relative_path = lower(relative_path)
where relative_path <> lower(relative_path);

update dblive.migration
set relative_path = lower(relative_path)
where relative_path <> lower(relative_path);
