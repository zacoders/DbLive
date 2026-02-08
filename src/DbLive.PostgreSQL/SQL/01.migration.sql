-- create schema
create schema if not exists dblive;


create table dblive.migration (
    version int not null,
    item_type varchar(32) not null,
    name text not null,
    relative_path text not null,
    status varchar(32) not null,
    content_hash bigint not null,
    content text null,
    created_utc timestamptz not null,
    applied_utc timestamptz null,
    execution_time_ms int null,
    error text null,

    constraint pk_dblive_migration primary key (version, item_type)
);

create table dblive.dbversion (
    version int not null,
    created_utc timestamptz not null,
    applied_utc timestamptz null,
    one_row_lock int not null default 1,

    constraint ck_dblive_dbversion_singleton check (one_row_lock = 1),
    constraint pk_dblive_dbversion primary key (one_row_lock)    
);

insert into dblive.dbversion (version, created_utc)
values (0, now());

create table dblive.version (
    version int not null,
    created_utc timestamptz not null,
    applied_utc timestamptz null,
    one_row_lock int not null default 1,

    constraint ck_dblive_version_singleton check (one_row_lock = 1),
    constraint pk_dblive_version primary key (one_row_lock)
);

insert into dblive.version (version, created_utc)
values (0, now());


create table dblive.code (
    relative_path text not null,
    status varchar(32) not null,
    execution_time_ms int not null,
    applied_utc timestamptz null,
    created_utc timestamptz not null,
    verified_utc timestamptz null,
    error text null,
    content_hash bigint not null,

    constraint pk_dblive_code primary key (relative_path)
);


create table dblive.unit_test_run (
    relative_path text not null,
    content_hash bigint not null,
    run_utc timestamptz not null,
    execution_time_ms int not null,
    pass boolean not null,
    error text null,

    constraint pk_unit_tests_results primary key (relative_path)
);


create table dblive.folder_items (
    folder_type varchar(32) not null,
    relative_path text not null,
    content_hash bigint not null,
    created_utc timestamptz not null,
    started_utc timestamptz not null,
    completed_utc timestamptz not null,
    execution_time_ms int not null,

    constraint pk_dblive_folders primary key (folder_type, relative_path)
);
