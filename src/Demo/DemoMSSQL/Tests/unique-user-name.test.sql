

declare @ExceptionThrows bit = 0
declare @ErrorMessage nvarchar(max)

begin try

	declare @DupUserName nvarchar(255) = newid()

	insert into dbo.Users ( Name )
	values ( @DupUserName )

	insert into dbo.Users ( Name )
	values ( @DupUserName )

end try
begin catch

	set @ExceptionThrows = 1
	set @ErrorMessage = error_message()

end catch

--print(@ErrorMessage)

if @ExceptionThrows = 0 or 
   @ErrorMessage not like '%IDX_Users_Name%'
	throw 50001, 'Unique index violation is expected.', 0;
