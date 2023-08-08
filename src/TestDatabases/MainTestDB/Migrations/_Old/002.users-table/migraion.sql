
create table dbo.Users (
    UserId int identity
  , Name nvarchar(128) not null
  , constraint PK_Users primary key clustered ( UserId )
)
