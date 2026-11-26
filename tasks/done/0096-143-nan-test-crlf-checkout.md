---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# A PostgreSQL test of NaN fails in a Windows checkout with CRLF

## Release note
None. Tests only.

## Problem
`SqlTestResultSetTests.Rows_test_compares_NaN_and_infinity` (PostgreSQL) fails in a Windows checkout with `core.autocrlf = true` and passes on CI. Found in 0096-142.

The expected start of the message is a raw string literal, and the message is compared after `ReplaceLineEndings("\n")`. A raw literal takes the line ends of the source file: verified, the file is LF in the index and CRLF in the working tree (`git ls-files --eol`: `i/lf w/crlf`), so the expected text has `\r\n` and the message `\n`.

## Where
`src/DbLive.PostgreSQL.Tests/SqlTestResultSetTests.cs`, `Rows_test_compares_NaN_and_infinity`.

## Fix
Normalize both sides with `ReplaceLineEndings()`, as the other tests that compare a message with a raw literal do (`CodeFileFailureTests`, `CodeBreakingTests`, `FolderDeployerTests`).

## Done when
- [x] The test passes in a CRLF checkout

## Result
The expected literal and the message are both normalized with `ReplaceLineEndings()`. Verified: `SqlTestResultSetTests` of PostgreSQL pass (3 of 3) in the Windows CRLF working tree.
