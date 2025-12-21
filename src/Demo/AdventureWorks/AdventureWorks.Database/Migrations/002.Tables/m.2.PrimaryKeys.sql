
-- ******************************************************
-- Add Primary Keys
-- ******************************************************
PRINT '';
PRINT '*** Adding Primary Keys';
GO

SET QUOTED_IDENTIFIER ON;

ALTER TABLE [Person].[Address] WITH CHECK ADD
    CONSTRAINT [PK_Address_AddressID] PRIMARY KEY CLUSTERED
    (
        [AddressID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Person].[AddressType] WITH CHECK ADD
    CONSTRAINT [PK_AddressType_AddressTypeID] PRIMARY KEY CLUSTERED
    (
        [AddressTypeID]
    )  ON [PRIMARY];
GO

ALTER TABLE [dbo].[AWBuildVersion] WITH CHECK ADD
    CONSTRAINT [PK_AWBuildVersion_SystemInformationID] PRIMARY KEY CLUSTERED
    (
        [SystemInformationID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[BillOfMaterials] WITH CHECK ADD
    CONSTRAINT [PK_BillOfMaterials_BillOfMaterialsID] PRIMARY KEY NONCLUSTERED
    (
        [BillOfMaterialsID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Person].[BusinessEntity] WITH CHECK ADD
    CONSTRAINT [PK_BusinessEntity_BusinessEntityID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Person].[BusinessEntityAddress] WITH CHECK ADD
    CONSTRAINT [PK_BusinessEntityAddress_BusinessEntityID_AddressID_AddressTypeID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID],
		[AddressID],
		[AddressTypeID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Person].[BusinessEntityContact] WITH CHECK ADD
    CONSTRAINT [PK_BusinessEntityContact_BusinessEntityID_PersonID_ContactTypeID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID],
		[PersonID],
		[ContactTypeID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Person].[ContactType] WITH CHECK ADD
    CONSTRAINT [PK_ContactType_ContactTypeID] PRIMARY KEY CLUSTERED
    (
        [ContactTypeID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[CountryRegionCurrency] WITH CHECK ADD
    CONSTRAINT [PK_CountryRegionCurrency_CountryRegionCode_CurrencyCode] PRIMARY KEY CLUSTERED
    (
        [CountryRegionCode],
        [CurrencyCode]
    )  ON [PRIMARY];
GO

ALTER TABLE [Person].[CountryRegion] WITH CHECK ADD
    CONSTRAINT [PK_CountryRegion_CountryRegionCode] PRIMARY KEY CLUSTERED
    (
        [CountryRegionCode]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[CreditCard] WITH CHECK ADD
    CONSTRAINT [PK_CreditCard_CreditCardID] PRIMARY KEY CLUSTERED
    (
        [CreditCardID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[Culture] WITH CHECK ADD
    CONSTRAINT [PK_Culture_CultureID] PRIMARY KEY CLUSTERED
    (
        [CultureID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[Currency] WITH CHECK ADD
    CONSTRAINT [PK_Currency_CurrencyCode] PRIMARY KEY CLUSTERED
    (
        [CurrencyCode]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[CurrencyRate] WITH CHECK ADD
    CONSTRAINT [PK_CurrencyRate_CurrencyRateID] PRIMARY KEY CLUSTERED
    (
        [CurrencyRateID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[Customer] WITH CHECK ADD
    CONSTRAINT [PK_Customer_CustomerID] PRIMARY KEY CLUSTERED
    (
        [CustomerID]
    )  ON [PRIMARY];
GO

ALTER TABLE [dbo].[DatabaseLog] WITH CHECK ADD
    CONSTRAINT [PK_DatabaseLog_DatabaseLogID] PRIMARY KEY NONCLUSTERED
    (
        [DatabaseLogID]
    )  ON [PRIMARY];
GO

ALTER TABLE [HumanResources].[Department] WITH CHECK ADD
    CONSTRAINT [PK_Department_DepartmentID] PRIMARY KEY CLUSTERED
    (
        [DepartmentID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[Document] WITH CHECK ADD
    CONSTRAINT [PK_Document_DocumentNode] PRIMARY KEY CLUSTERED
    (
        [DocumentNode]
    )  ON [PRIMARY];
GO

ALTER TABLE [Person].[EmailAddress] WITH CHECK ADD
    CONSTRAINT [PK_EmailAddress_BusinessEntityID_EmailAddressID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID],
		[EmailAddressID]
    )  ON [PRIMARY];
GO

ALTER TABLE [HumanResources].[Employee] WITH CHECK ADD
    CONSTRAINT [PK_Employee_BusinessEntityID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID]
    )  ON [PRIMARY];
GO

ALTER TABLE [HumanResources].[EmployeeDepartmentHistory] WITH CHECK ADD
    CONSTRAINT [PK_EmployeeDepartmentHistory_BusinessEntityID_StartDate_DepartmentID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID],
        [StartDate],
        [DepartmentID],
        [ShiftID]
    )  ON [PRIMARY];
GO

ALTER TABLE [HumanResources].[EmployeePayHistory] WITH CHECK ADD
    CONSTRAINT [PK_EmployeePayHistory_BusinessEntityID_RateChangeDate] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID],
        [RateChangeDate]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[Illustration] WITH CHECK ADD
    CONSTRAINT [PK_Illustration_IllustrationID] PRIMARY KEY CLUSTERED
    (
        [IllustrationID]
    )  ON [PRIMARY];
GO

ALTER TABLE [HumanResources].[JobCandidate] WITH CHECK ADD
    CONSTRAINT [PK_JobCandidate_JobCandidateID] PRIMARY KEY CLUSTERED
    (
        [JobCandidateID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[Location] WITH CHECK ADD
    CONSTRAINT [PK_Location_LocationID] PRIMARY KEY CLUSTERED
    (
        [LocationID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Person].[Password] WITH CHECK ADD
    CONSTRAINT [PK_Password_BusinessEntityID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Person].[Person] WITH CHECK ADD
    CONSTRAINT [PK_Person_BusinessEntityID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[PersonCreditCard] WITH CHECK ADD
    CONSTRAINT [PK_PersonCreditCard_BusinessEntityID_CreditCardID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID],
        [CreditCardID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Person].[PersonPhone] WITH CHECK ADD
    CONSTRAINT [PK_PersonPhone_BusinessEntityID_PhoneNumber_PhoneNumberTypeID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID],
        [PhoneNumber],
        [PhoneNumberTypeID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Person].[PhoneNumberType] WITH CHECK ADD
    CONSTRAINT [PK_PhoneNumberType_PhoneNumberTypeID] PRIMARY KEY CLUSTERED
    (
        [PhoneNumberTypeID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[Product] WITH CHECK ADD
    CONSTRAINT [PK_Product_ProductID] PRIMARY KEY CLUSTERED
    (
        [ProductID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductCategory] WITH CHECK ADD
    CONSTRAINT [PK_ProductCategory_ProductCategoryID] PRIMARY KEY CLUSTERED
    (
        [ProductCategoryID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductCostHistory] WITH CHECK ADD
    CONSTRAINT [PK_ProductCostHistory_ProductID_StartDate] PRIMARY KEY CLUSTERED
    (
        [ProductID],
        [StartDate]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductDescription] WITH CHECK ADD
    CONSTRAINT [PK_ProductDescription_ProductDescriptionID] PRIMARY KEY CLUSTERED
    (
        [ProductDescriptionID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductDocument] WITH CHECK ADD
    CONSTRAINT [PK_ProductDocument_ProductID_DocumentNode] PRIMARY KEY CLUSTERED
    (
        [ProductID],
        [DocumentNode]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductInventory] WITH CHECK ADD
    CONSTRAINT [PK_ProductInventory_ProductID_LocationID] PRIMARY KEY CLUSTERED
    (
    [ProductID],
    [LocationID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductListPriceHistory] WITH CHECK ADD
    CONSTRAINT [PK_ProductListPriceHistory_ProductID_StartDate] PRIMARY KEY CLUSTERED
    (
        [ProductID],
        [StartDate]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductModel] WITH CHECK ADD
    CONSTRAINT [PK_ProductModel_ProductModelID] PRIMARY KEY CLUSTERED
    (
        [ProductModelID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductModelIllustration] WITH CHECK ADD
    CONSTRAINT [PK_ProductModelIllustration_ProductModelID_IllustrationID] PRIMARY KEY CLUSTERED
    (
        [ProductModelID],
        [IllustrationID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductModelProductDescriptionCulture] WITH CHECK ADD
    CONSTRAINT [PK_ProductModelProductDescriptionCulture_ProductModelID_ProductDescriptionID_CultureID] PRIMARY KEY CLUSTERED
    (
        [ProductModelID],
        [ProductDescriptionID],
        [CultureID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductPhoto] WITH CHECK ADD
    CONSTRAINT [PK_ProductPhoto_ProductPhotoID] PRIMARY KEY CLUSTERED
    (
        [ProductPhotoID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductProductPhoto] WITH CHECK ADD
    CONSTRAINT [PK_ProductProductPhoto_ProductID_ProductPhotoID] PRIMARY KEY NONCLUSTERED
    (
        [ProductID],
        [ProductPhotoID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductReview] WITH CHECK ADD
    CONSTRAINT [PK_ProductReview_ProductReviewID] PRIMARY KEY CLUSTERED
    (
        [ProductReviewID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ProductSubcategory] WITH CHECK ADD
    CONSTRAINT [PK_ProductSubcategory_ProductSubcategoryID] PRIMARY KEY CLUSTERED
    (
        [ProductSubcategoryID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Purchasing].[ProductVendor] WITH CHECK ADD
    CONSTRAINT [PK_ProductVendor_ProductID_BusinessEntityID] PRIMARY KEY CLUSTERED
    (
        [ProductID],
        [BusinessEntityID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Purchasing].[PurchaseOrderDetail] WITH CHECK ADD
    CONSTRAINT [PK_PurchaseOrderDetail_PurchaseOrderID_PurchaseOrderDetailID] PRIMARY KEY CLUSTERED
    (
        [PurchaseOrderID],
        [PurchaseOrderDetailID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Purchasing].[PurchaseOrderHeader] WITH CHECK ADD
    CONSTRAINT [PK_PurchaseOrderHeader_PurchaseOrderID] PRIMARY KEY CLUSTERED
    (
        [PurchaseOrderID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[SalesOrderDetail] WITH CHECK ADD
    CONSTRAINT [PK_SalesOrderDetail_SalesOrderID_SalesOrderDetailID] PRIMARY KEY CLUSTERED
    (
        [SalesOrderID],
        [SalesOrderDetailID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[SalesOrderHeader] WITH CHECK ADD
    CONSTRAINT [PK_SalesOrderHeader_SalesOrderID] PRIMARY KEY CLUSTERED
    (
        [SalesOrderID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[SalesOrderHeaderSalesReason] WITH CHECK ADD
    CONSTRAINT [PK_SalesOrderHeaderSalesReason_SalesOrderID_SalesReasonID] PRIMARY KEY CLUSTERED
    (
        [SalesOrderID],
        [SalesReasonID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[SalesPerson] WITH CHECK ADD
    CONSTRAINT [PK_SalesPerson_BusinessEntityID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[SalesPersonQuotaHistory] WITH CHECK ADD
    CONSTRAINT [PK_SalesPersonQuotaHistory_BusinessEntityID_QuotaDate] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID],
        [QuotaDate] --,
        -- [ProductCategoryID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[SalesReason] WITH CHECK ADD
    CONSTRAINT [PK_SalesReason_SalesReasonID] PRIMARY KEY CLUSTERED
    (
        [SalesReasonID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[SalesTaxRate] WITH CHECK ADD
    CONSTRAINT [PK_SalesTaxRate_SalesTaxRateID] PRIMARY KEY CLUSTERED
    (
        [SalesTaxRateID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[SalesTerritory] WITH CHECK ADD
    CONSTRAINT [PK_SalesTerritory_TerritoryID] PRIMARY KEY CLUSTERED
    (
        [TerritoryID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[SalesTerritoryHistory] WITH CHECK ADD
    CONSTRAINT [PK_SalesTerritoryHistory_BusinessEntityID_StartDate_TerritoryID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID],  --Sales person
        [StartDate],
        [TerritoryID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[ScrapReason] WITH CHECK ADD
    CONSTRAINT [PK_ScrapReason_ScrapReasonID] PRIMARY KEY CLUSTERED
    (
        [ScrapReasonID]
    )  ON [PRIMARY];
GO

ALTER TABLE [HumanResources].[Shift] WITH CHECK ADD
    CONSTRAINT [PK_Shift_ShiftID] PRIMARY KEY CLUSTERED
    (
        [ShiftID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Purchasing].[ShipMethod] WITH CHECK ADD
    CONSTRAINT [PK_ShipMethod_ShipMethodID] PRIMARY KEY CLUSTERED
    (
        [ShipMethodID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[ShoppingCartItem] WITH CHECK ADD
    CONSTRAINT [PK_ShoppingCartItem_ShoppingCartItemID] PRIMARY KEY CLUSTERED
    (
        [ShoppingCartItemID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[SpecialOffer] WITH CHECK ADD
    CONSTRAINT [PK_SpecialOffer_SpecialOfferID] PRIMARY KEY CLUSTERED
    (
        [SpecialOfferID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[SpecialOfferProduct] WITH CHECK ADD
    CONSTRAINT [PK_SpecialOfferProduct_SpecialOfferID_ProductID] PRIMARY KEY CLUSTERED
    (
        [SpecialOfferID],
        [ProductID]
    )  ON [PRIMARY];
GO
GO

ALTER TABLE [Person].[StateProvince] WITH CHECK ADD
    CONSTRAINT [PK_StateProvince_StateProvinceID] PRIMARY KEY CLUSTERED
    (
        [StateProvinceID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Sales].[Store] WITH CHECK ADD
    CONSTRAINT [PK_Store_BusinessEntityID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[TransactionHistory] WITH CHECK ADD
    CONSTRAINT [PK_TransactionHistory_TransactionID] PRIMARY KEY CLUSTERED
    (
        [TransactionID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[TransactionHistoryArchive] WITH CHECK ADD
    CONSTRAINT [PK_TransactionHistoryArchive_TransactionID] PRIMARY KEY CLUSTERED
    (
        [TransactionID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[UnitMeasure] WITH CHECK ADD
    CONSTRAINT [PK_UnitMeasure_UnitMeasureCode] PRIMARY KEY CLUSTERED
    (
        [UnitMeasureCode]
    )  ON [PRIMARY];
GO

ALTER TABLE [Purchasing].[Vendor] WITH CHECK ADD
    CONSTRAINT [PK_Vendor_BusinessEntityID] PRIMARY KEY CLUSTERED
    (
        [BusinessEntityID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[WorkOrder] WITH CHECK ADD
    CONSTRAINT [PK_WorkOrder_WorkOrderID] PRIMARY KEY CLUSTERED
    (
        [WorkOrderID]
    )  ON [PRIMARY];
GO

ALTER TABLE [Production].[WorkOrderRouting] WITH CHECK ADD
    CONSTRAINT [PK_WorkOrderRouting_WorkOrderID_ProductID_OperationSequence] PRIMARY KEY CLUSTERED
    (
        [WorkOrderID],
        [ProductID],
        [OperationSequence]
    )  ON [PRIMARY];
GO