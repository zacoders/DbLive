
-- ****************************************
-- Create XML index for each XML column
-- ****************************************
PRINT '';
PRINT '*** Creating XML index for each XML column';
GO

SET ARITHABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

CREATE PRIMARY XML INDEX [PXML_Person_AddContact] ON [Person].[Person]([AdditionalContactInfo]);
GO

CREATE PRIMARY XML INDEX [PXML_Person_Demographics] ON [Person].[Person]([Demographics]);
GO

CREATE XML INDEX [XMLPATH_Person_Demographics] ON [Person].[Person]([Demographics])
USING XML INDEX [PXML_Person_Demographics] FOR PATH;
GO

CREATE XML INDEX [XMLPROPERTY_Person_Demographics] ON [Person].[Person]([Demographics])
USING XML INDEX [PXML_Person_Demographics] FOR PROPERTY;
GO

CREATE XML INDEX [XMLVALUE_Person_Demographics] ON [Person].[Person]([Demographics])
USING XML INDEX [PXML_Person_Demographics] FOR VALUE;
GO

CREATE PRIMARY XML INDEX [PXML_Store_Demographics] ON [Sales].[Store]([Demographics]);
GO

CREATE PRIMARY XML INDEX [PXML_ProductModel_CatalogDescription] ON [Production].[ProductModel]([CatalogDescription]);
GO

CREATE PRIMARY XML INDEX [PXML_ProductModel_Instructions] ON [Production].[ProductModel]([Instructions]);
GO