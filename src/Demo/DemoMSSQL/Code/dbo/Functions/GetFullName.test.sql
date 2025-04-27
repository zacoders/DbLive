-- will be executed in transaction with rollback.
-- no exceptions expected.

select dbo.GetFullName('first', 'last')


select assert = 'rows-with-schema'
select cast('first last' as nvarchar(255))