-- A test of get_user. It runs in a transaction that is always rolled back.

-- Arrange
insert into users ( user_id, name ) values ( 1000, 'test-user' );

-- Act
select user_id, name from get_user(1000);

-- Assert: the result sets before the assert are the actual result, the ones after it the expected result.
select 'rows' as assert;
select 1000 as user_id, 'test-user' as name;
