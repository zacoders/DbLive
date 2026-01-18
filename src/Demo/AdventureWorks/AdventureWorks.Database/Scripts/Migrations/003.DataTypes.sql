

-- ****************************************
-- Create Data Types
-- ****************************************


CREATE TYPE [AccountNumber] FROM nvarchar(15) NULL;
CREATE TYPE [Flag] FROM bit NOT NULL;
CREATE TYPE [NameStyle] FROM bit NOT NULL;
CREATE TYPE [Name] FROM nvarchar(50) NULL;
CREATE TYPE [OrderNumber] FROM nvarchar(25) NULL;
CREATE TYPE [Phone] FROM nvarchar(25) NULL;
GO
