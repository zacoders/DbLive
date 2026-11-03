-- A data rule: a user must have a name. Without an assert, the test passes when the script raises no error.
begin try
    insert into dbo.Users ( Name ) values ( N'' );
    throw 50000, 'dbo.Users accepted a user with an empty name.', 1;
end try
begin catch
    if error_number() <> 547 throw; -- 547: the check constraint CK_Users_Name rejected the row, as expected
end catch;
