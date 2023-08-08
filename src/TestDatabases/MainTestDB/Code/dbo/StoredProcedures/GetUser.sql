
create or alter proc dbo.GetUser
    @UserId int
as 
    
    select *
    from dbo.Users
    where UserId = @UserId

go
