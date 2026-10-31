-- relative paths are stored with '/' on every OS; DbLive on Windows used to store them with '\'.
-- a database deployed from both Windows and Linux has two rows for one file: the latest row is kept.

delete from dblive.code
where ctid in (
    select ctid
    from (
        select ctid,
               row_number() over (
                   partition by replace(relative_path, '\', '/')
                   order by greatest(applied_utc, verified_utc, created_utc) desc
               ) as rn
        from dblive.code
    ) r
    where rn > 1
);

update dblive.code
set relative_path = replace(relative_path, '\', '/')
where strpos(relative_path, '\') > 0;

delete from dblive.unit_test_run
where ctid in (
    select ctid
    from (
        select ctid, row_number() over (partition by replace(relative_path, '\', '/') order by run_utc desc) as rn
        from dblive.unit_test_run
    ) r
    where rn > 1
);

update dblive.unit_test_run
set relative_path = replace(relative_path, '\', '/')
where strpos(relative_path, '\') > 0;

delete from dblive.folder_items
where ctid in (
    select ctid
    from (
        select ctid, row_number() over (partition by folder_type, replace(relative_path, '\', '/') order by completed_utc desc) as rn
        from dblive.folder_items
    ) r
    where rn > 1
);

update dblive.folder_items
set relative_path = replace(relative_path, '\', '/')
where strpos(relative_path, '\') > 0;

update dblive.migration
set relative_path = replace(relative_path, '\', '/')
where strpos(relative_path, '\') > 0;
