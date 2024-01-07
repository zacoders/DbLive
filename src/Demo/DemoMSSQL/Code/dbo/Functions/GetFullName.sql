
create or alter function dbo.GetFullName
(
    @FirstName nvarchar(128)
  , @LastName nvarchar(123)
)
returns nvarchar(255)
as
begin
    return trim(concat(@FirstName, ' ', @LastName))
end