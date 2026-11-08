---
type: bug
priority: low
created: 2026-11-08
---
# The error of a BeforeDeploy or AfterDeploy script does not name the file

## Problem
`FolderDeployer` does not wrap the error of a script: `DbLiveSqlException` leaves the deployment with the SQL error only, for example "Invalid object name 'dbo.X'.", without the path of the file. Migrations (`MigrationDeploymentException`) and code files (`CodeDeploymentException`) name the file. With several scripts in `AfterDeploy` the file is found only by the `Information` line "Deploying item: ..." before the error, which is not there with a higher log level.

Found by the code review of 2026-11-08 (finding 6).

## Where
- `src/DbLive/Deployers/Folder/FolderDeployer.cs:42-51`

## Fix
Proposal: catch the error of the script and throw an exception that names the file and has the error as its inner exception, like the other deployers.

- [ ] A new exception type (`FolderDeploymentException`?) or an existing one? A user who catches `DbLiveSqlException` of these folders would see another type: `breaking: true`?

## Docs to update
- `docs/llms-full.txt` §7, §17
- `docs/project/structure.md` ("BeforeDeploy and AfterDeploy")

## Done when
- [ ] Fixed, with a unit test
- [ ] Docs updated
