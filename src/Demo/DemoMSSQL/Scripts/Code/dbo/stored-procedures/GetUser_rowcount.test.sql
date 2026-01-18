
-- Arrange
set identity_insert dbo.Users on;

insert into dbo.Users ( UserId, Name )
values ( 10, 'TestUser10' )

-- Act
exec dbo.GetUser2 @UserId = -999


-- Assert
select assert = 'row-count=0' 
