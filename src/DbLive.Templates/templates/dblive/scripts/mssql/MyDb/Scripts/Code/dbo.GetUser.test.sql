-- A test of dbo.GetUser. It runs in a transaction that is always rolled back.

-- Arrange
set identity_insert dbo.Users on;
insert into dbo.Users ( UserId, Name ) values ( 1000, N'test-user' );
set identity_insert dbo.Users off;

-- Act
exec dbo.GetUser @UserId = 1000;

-- Assert: the result sets before the assert are the actual result, the ones after it the expected result.
select assert = 'rows';
select 1000 as UserId, N'test-user' as Name;
