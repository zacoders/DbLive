---
type: docs
priority: high
created: 2026-06-28
---
# Documentation

Write the user documentation (`docs/`, published with mkdocs) and the reference for AI agents (`docs/llms.txt`, `docs/llms-full.txt`, `docs/ai-agents.md`). AI agents are the primary readers: `docs/llms-full.txt` is the source of facts, and the pages under `docs/` tell the same in narrative form.

Branch: `96-documentation`.

The maintainer writes `docs/philosophy.md`, `docs/database-as-service.md` and `docs/deployment/zero-downtime.md` by hand: fix links there, but ask before rewriting text.

## Remaining work

`mkdocs build` gives 15 warnings, all from `philosophy.md` and `deployment/zero-downtime.md` (D1, D2).

### D1. Broken links in `docs/philosophy.md`
- [ ] `project-id.md` → `features/project-id.md`.
- [ ] Links to `../README.md#...` and `../src/...` don't work on the site. Replace them with site pages (`gettingstarted.md`, `deployment/index.md`, `unit-testing/index.md`) and absolute GitHub links.

### D2. Broken links in `docs/deployment/zero-downtime.md`
- [ ] `../../README.md#deployment-flow` → `deployment/index.md`.
- [ ] Links to `../../src/...` → absolute GitHub links.

### D3. Empty page `docs/database-as-service.md`
The page has only a heading, but `zero-downtime.md` links to it and it shows in the navigation.
- [ ] Either move the "Database as a service" section from `philosophy.md` there, or remove the page and the link.

### D4. No `nav:` in `mkdocs.yml`
Without it pages are sorted alphabetically, so `database-as-service` comes before Getting Started.
- [ ] Add `nav:` in this order: Home, Getting Started, Philosophy, Project, Deployment, Unit Testing, Features, Examples, For AI agents.

### D5. Review the new documentation
The new pages are committed (fa118f6).
- [ ] Proofread `docs/llms-full.txt` first: it is the source of facts for agents.
- [ ] After merging to `main`, check that the links from `llms.txt` to `raw.githubusercontent.com/.../main/` work.

### D6. Keep the docs from going stale
- [ ] After D1 and D2, run `mkdocs build --strict` in `.github/workflows/docs.yml`, so a broken link fails the build.
- [ ] Add a PR template (none exists yet) with the item "behavior changed → `docs/llms-full.txt` and the page under `docs/` updated".

## Found during this task

Bugs found while studying the code for the docs are tasks `0096-01` to `0096-11` (`ls tasks/0096-* tasks/done/0096-*`). The docs describe current behavior, so each fix updates them in the same commit.

## Done when
- [ ] D1–D6 done
- [ ] `mkdocs build --strict` passes
- [ ] Branch merged to `main`
