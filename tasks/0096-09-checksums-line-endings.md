---
type: bug
priority: medium
created: 2026-10-26
---
# Checksums depend on line endings and path separators

## Problem
The hash is computed over the raw file text. On Windows with `core.autocrlf=true` files have CRLF, on a Linux CI they have LF, so an applied migration fails with `MigrationChecksumMismatchException`. The repository has no `.gitattributes`. Likewise `relative_path` is stored with the OS separator, so switching OS redeploys all code once.

## Where
`src/DbLive/Common/Hasher.cs:8`, `src/DbLive/Common/FileSystem.cs:62`.

## Fix
- Option A: normalize `\r\n` to `\n` before hashing. Hashes of already applied CRLF migrations change, so a transition is needed (accept both hashes).
- Option B: keep the code, add `.gitattributes` (`*.sql text eol=lf`) and recommend it in the project template.

In both cases store `relative_path` with `/`.

- [ ] Choose A or B.

## Docs to update
- `docs/llms-full.txt` §5.4 (Line endings paragraph), §18.1 (step 5), §19
- `docs/project/structure.md` (section "Line endings")
- `docs/deployment/migrations.md` (section "Checksums")

## Done when
- [ ] Chosen option implemented, with unit tests
- [ ] `relative_path` stored with `/`
- [ ] Docs updated
