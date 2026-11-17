-- A code object: deployed after the migrations, and again whenever this file changes.
create or alter proc dbo.GetUser
    @UserId int
as
    select UserId, Name
    from dbo.Users
    where UserId = @UserId;
