-- will be executed in transaction with rollback.

set identity_insert dbo.Users on;

insert into dbo.Users ( UserId, Name )
values ( 10, 'TestUser10' )

exec dbo.GetUser2 @UserId = -999


select assert = 'row-count=0' 
