-- will be executed in transaction with rollback.
-- no exceptions expected.
exec dbo.GetUser @UserId = 1
