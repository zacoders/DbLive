

declare @cnt int = (select count(*) from dbo.Orders)

if @cnt != 3
begin
	declare @msg nvarchar(max) = concat('3 orders expected, but found ', @cnt);
	throw 50001, @msg, 0;
end

