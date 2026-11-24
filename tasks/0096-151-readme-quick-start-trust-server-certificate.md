---
type: docs
priority: low
created: 2026-11-24
---
# The quick start of `README.md` does not connect to a local SQL Server

## Problem
`README.md:131`: `string dbCnnString = "Server=localhost;Database=MyDb;Trusted_Connection=True;";`. Microsoft.Data.SqlClient 6 (`DbLive.MSSQL` brings 6.1.4) encrypts by default, and a local SQL Server with its self-signed certificate rejects the connection ("The certificate chain was issued by an authority that is not trusted"). Every other snippet has `TrustServerCertificate=True`: `docs/llms-full.txt` §3.3, the README of `DbLive.MSSQL`, the `Program.cs` of the template, `docs/unit-testing/index.md`; `docs/gettingstarted.md:131` explains it in a note. Not run: the SqlClient default.

## Docs to update
- `README.md:131`: add `TrustServerCertificate=True;`

## Done when
- [ ] Docs updated

## Notes
- Found by the code review of 2026-11-24.
