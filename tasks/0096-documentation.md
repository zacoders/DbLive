---
type: docs
priority: high
created: 2026-06-28
---
# Documentation

Write the user documentation (`docs/`, published with mkdocs) and the reference for AI agents (`docs/llms.txt`, `docs/llms-full.txt`, `docs/ai-agents.md`). AI agents are the primary readers: `docs/llms-full.txt` is the source of facts, and the pages under `docs/` tell the same in narrative form.

Branch: `96-documentation`.

The maintainer writes `docs/database-as-service.md` and `docs/deployment/zero-downtime.md` by hand: fix links there, but ask before rewriting text.

## Remaining work

`mkdocs build --strict` passes since D1–D4.

### D1. Broken links in `docs/philosophy.md`
- [x] `philosophy.md` merged into `database-as-service.md` (see Notes); its links now point to site pages and absolute GitHub links.

### D2. Broken links in `docs/deployment/zero-downtime.md`
- [x] `../../README.md#deployment-flow` → `deployment/index.md`.
- [x] Links to `../../src/...` → absolute GitHub links.

### D3. Empty page `docs/database-as-service.md`
- [x] Filled from `philosophy.md`; `philosophy.md` removed and all links updated.

### D4. No `nav:` in `mkdocs.yml`
- [x] Added: Home, Getting Started, Database as a Service, Project, Deployment, Unit Testing, Features, Examples, For AI agents.

### D5. Review the new documentation
The new pages are committed (fa118f6).
- [ ] Proofread `docs/llms-full.txt` first: it is the source of facts for agents.
- [ ] After merging to `main`, check that the links from `llms.txt` to `raw.githubusercontent.com/.../main/` work.

### D6. Keep the docs from going stale
- [ ] After D1 and D2, run `mkdocs build --strict` in `.github/workflows/docs.yml`, so a broken link fails the build.
- [ ] Add a PR template (none exists yet) with the item "behavior changed → `docs/llms-full.txt` and the page under `docs/` updated".

## Found during this task

Bugs found while studying the code for the docs are tasks `0096-01` to `0096-16` (`ls tasks/0096-* tasks/done/0096-*`). The docs describe current behavior, so each fix updates them in the same commit.

## Done when
- [ ] D1–D6 done
- [x] `mkdocs build --strict` passes
- [ ] Branch merged to `main`

## Notes

2026-10-31, docs structure agreed with the maintainer:
- "Database as a service" is the core idea of DbLive and its philosophy, so there is one page for it: `docs/database-as-service.md`. `philosophy.md` was merged into it and removed.
- The model is optional. The page has "Ways to use DbLive": classic (migrations and code), zero downtime, database as a service. SQL tests, rollback, checksums and ProjectId are extras available in every way. DbLive encourages zero downtime; transactions per migration and scripts without a transaction are part of that.
- The interface: procedures and functions are recommended; views and direct table access are possible but make seamless releases harder.
- A procedure is a module with a history: one file, changed in place. A contract change renames the object to the next version in the same file, and the old version is dropped in a breaking script; an implementation change keeps the name.
- Moving data in the background is an example of how a migration can expand the schema (`examples/table-split.md`), not a DbLive feature.
- One home per fact: the idea, the model and the release cycle diagram live in `database-as-service.md`; steps, CI/CD and the checklist in `deployment/zero-downtime.md`; presets in `deployment/index.md`; transactions in `project/configuration.md`; the 007 walkthrough in `examples/table-split.md` (aligned with the demo: `Person`/`PersonPhones`). Other pages link.
- `docs/llms-full.txt`: rule 14 (follow the way the project uses DbLive; contract vs implementation change), ways table in Section 1, AfterDeploy is not for breaking changes (Section 7).
- Open: where to drop an old procedure version when the release has no migration → task 0096-15.
