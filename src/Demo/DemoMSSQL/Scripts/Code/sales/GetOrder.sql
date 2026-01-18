

create or alter proc sales.GetOrder
	@OrderId int
as

	select *
	from dbo.Orders
	where OrderId = @OrderId

go

