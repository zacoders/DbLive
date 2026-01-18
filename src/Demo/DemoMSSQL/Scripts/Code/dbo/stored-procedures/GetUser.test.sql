
-- Arrange
set identity_insert dbo.Users on;

insert into dbo.Users ( UserId, Name )
values ( 10, 'TestUser10' )

-- Act
exec dbo.GetUser2 @UserId = 10


-- Assert
select assert = 'rows'

select 10 as UserId, 'TestUser10' as Name
