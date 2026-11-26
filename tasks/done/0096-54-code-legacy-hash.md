---
type: chore
priority: low
created: 2026-11-07
closed: 2026-11-07
---
# Code files are compared with the current hash only

## Release note
None. The release note of 0096-09 says it: a code file that DbLive 1.x deployed from a Windows checkout runs once more on the first deployment.

## Problem
`CodeRows.IsDeployed` compared the stored hash of a code file with `FileData.MatchesHash`, which also accepts the hashes of DbLive 1.x (the raw text of a CRLF checkout, and the raw text itself). A match by such a hash set only `verified_utc` and left the old hash in the row, so the fallback ran on every deployment for every such file (three hashes instead of one) and could never be removed.

Found by the code review of 2026-11-07 (finding 13).

## Decision
2026-11-07, maintainer: the legacy hashes do not matter for code; check only the current hash. The code file is deployed once more, which is not a problem.

## Where
`src/DbLive/Deployers/Code/CodeRows.cs` (used by the code deployment and by the guard of the breaking phase).

## Docs to update
- `docs/llms-full.txt` §5.4 (line endings)
- `docs/deployment/code.md` ("How it works")
- The release note of 0096-09

## Done when
- [x] Code compares the current hash only, tests updated
- [x] Docs updated

## Result
- `CodeRows.IsDeployed` compares `row.ContentHash == fileData.ContentHash`. The code file runs once and its row gets the current hash, so the next deployment skips it. Migrations keep `MatchesHash`: an applied migration must keep matching (`MigrationChecksumMismatchException` otherwise).
- Tests: `CodeIDeployerTests.DeployCode_Skips_Deployed_Files_And_Verifies_Them_With_One_Command` deploys the file with a legacy hash; the guard test of `BreakingChangesDeployerTests` uses the current hash; `CrossPlatformDeployTests` on both providers checks that the code file deployed by an older DbLive from Windows runs once more and gets the current hash.
