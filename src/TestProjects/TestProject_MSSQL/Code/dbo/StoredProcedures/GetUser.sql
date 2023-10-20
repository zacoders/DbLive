
create or alter proc dbo.GetUser2
    @UserId int
as 
    
    select *
    from dbo.Users
    where UserId = @UserId

go
