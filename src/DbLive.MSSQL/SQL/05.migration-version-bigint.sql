alter table dblive.migration drop constraint pk_dblive_migration;
go

alter table dblive.migration alter column version bigint not null;
go

alter table dblive.migration add constraint pk_dblive_migration primary key ( version, item_type );
go

drop table dblive.dbversion;
go

alter table dblive.version alter column version bigint not null;
go