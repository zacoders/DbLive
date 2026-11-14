-- GetUser.sql created dbo.GetUser until its contract changed; now it creates dbo.GetUser2.
-- The old version is dropped in the breaking phase, after the applications that call it are gone.
-- The script runs on every breaking deployment, so it must be idempotent.

drop proc if exists dbo.GetUser
go
