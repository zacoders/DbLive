-- will be executed in transaction with rollback.
-- no exceptions expected.

if dbo.GetFullName('first', 'last') != 'first last'
	throw 50001, 'Expected "first last"', 0;

