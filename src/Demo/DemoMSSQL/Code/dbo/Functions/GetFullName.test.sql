-- will be executed in transaction with rollback.
-- no exceptions expected.

select dbo.GetFullName('first', 'last')


select expected = 'rows'
select cast('first last' as nvarchar(255))