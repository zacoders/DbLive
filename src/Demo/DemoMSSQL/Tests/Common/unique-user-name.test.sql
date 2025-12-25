
-- Arrange
declare @DupUserName nvarchar(255) = newid()

insert into dbo.Users ( Name )
values ( @DupUserName )


-- Act
declare @ErrorMessage nvarchar(max)

begin try	
	insert into dbo.Users ( Name )
	values ( @DupUserName )
end try
begin catch
	set @ErrorMessage = error_message()
end catch

-- Assert
if @ErrorMessage not like '%IDX_Users_Name%'
	throw 50001, 'Unique index violation is expected.', 0;
