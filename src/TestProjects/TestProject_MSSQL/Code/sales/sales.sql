
if schema_id('sales') is null
begin
	exec('create schema sales')
end
