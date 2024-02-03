-- will be executed in transaction with rollback.

set identity_insert dbo.Users on;

insert into dbo.Users ( UserId, Name )
values ( 10, 'TestUser10' )

exec dbo.GetUser2 @UserId = 10

-- TODO: this one of the options how we can check result of the stored procedure.

-- select expected = 'ordered-rows' -- result should match but order is not impotant. next select should contain expected rows.
-- select expected = 'rows' -- result should match to the next select statement. next select should contain expected rows.
-- select expected = 'any-row' -- no rows: exception.
-- select expected = 'single-row' -- only one rows must be returned. no rows: exception, more than one rows: exception.
-- select expected = 'rowcount:10' -- expected exact number of rows returned

select expected = 'rows'
select 10 as UserId, 'TestUser10' as Name
