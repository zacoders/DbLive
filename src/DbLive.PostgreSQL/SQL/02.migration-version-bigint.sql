alter table dblive.migration drop constraint pk_dblive_migration;

alter table dblive.migration alter column version type bigint;

alter table dblive.migration add constraint pk_dblive_migration primary key (version, item_type);

drop table if exists dblive.dbversion;

alter table dblive.version alter column version type bigint;
