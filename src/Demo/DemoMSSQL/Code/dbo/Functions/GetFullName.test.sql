-- will be executed in transaction with rollback.
-- no exceptions expected.

select dbo.GetFullName('first', 'last')


select expected = 'rows', types_check = 1
select cast('first last' as nvarchar(255))