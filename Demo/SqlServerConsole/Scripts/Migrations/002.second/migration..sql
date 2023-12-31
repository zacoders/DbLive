

create table dbo.Test2 (
	Id int
  , Name varchar(128)
)
go

insert into dbo.Test2 ( Id, Name ) 
values ( 1, 'test1' )
     , ( 2, 'second' )
go
