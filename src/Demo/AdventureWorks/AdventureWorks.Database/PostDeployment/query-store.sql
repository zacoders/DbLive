
declare @sql nvarchar(max) = concat('
	ALTER DATABASE [', db_name(), ']
	SET QUERY_STORE = ON
		(
			OPERATION_MODE = READ_WRITE,
			QUERY_CAPTURE_MODE = ALL,
			MAX_STORAGE_SIZE_MB = 100,
			INTERVAL_LENGTH_MINUTES = 30
		);
	')
exec(@sql)
go

