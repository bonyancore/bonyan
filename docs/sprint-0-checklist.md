# Sprint 0 checklist (weeks 1-2, walking skeleton)

Backend (from spec 20, section 12):
- [ ] Root files, CODEOWNERS, PR template, CI pipeline with all gates
- [ ] Shared: Kernel, Messaging, Persistence, Web, Testing (each with tests)
- [ ] Host: Api and Worker running locally
- [ ] Module template working; Platform and Core created from it
- [ ] Architecture.Tests rules 1 to 5 passing
- [ ] Reference feature (core project + work package) with outbox event `core.project.created.v1`
- [ ] GoldenProject.Tests running the plumbing example
- [ ] `contracts/openapi/v1.json` generated and diffed in CI

Web / Mobile:
- [ ] Decide framework tooling and project init (see each app README)
- [ ] Generate API client from `contracts/openapi/v1.json`
- [ ] Arabic-first RTL setup and message catalogue loading
- [ ] Auth flow against the backend (web: BFF cookie session; mobile: JWT + refresh in secure storage)

Open decisions to settle in Sprint 0 are listed in Notion: spec 00 section 13 and BE-12.
