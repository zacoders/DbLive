EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'AdventureWorksLT 2012 Sample OLTP Database';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Input parameter for the table value function ufnGetCustomerInformation. Enter a valid CustomerID from the Sales.Customer table.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'FUNCTION', @level1name = N'ufnGetCustomerInformation', @level2type = N'PARAMETER', @level2name = N'@CustomerID';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Input parameter for the scalar function ufnGetSalesOrderStatusText. Enter a valid integer.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'FUNCTION', @level1name = N'ufnGetSalesOrderStatusText', @level2type = N'PARAMETER', @level2name = N'@Status';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Output parameter for the stored procedure uspLogError. Contains the ErrorLogID value corresponding to the row inserted by uspLogError in the ErrorLog table.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'PROCEDURE', @level1name = N'uspLogError', @level2type = N'PARAMETER', @level2name = N'@ErrorLogID';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Primary filegroup for the AdventureWorks sample database.', @level0type = N'FILEGROUP', @level0name = N'PRIMARY';

