# Docusaurus docs site for oncall-busybar-agent, plus README dedup across all three BUSY Bar repos

## Motivation

`oncall-busybar-agent`'s README has grown to cover MSIX packaging, layered configuration,
Intune/Group Policy management, ADMX, and startup-toggle behavior — all accumulated during the
MSIX packaging work earlier in this session. It's the reference for all of that, but it's no
longer a README-sized document.

Separately, Microsoft Partner Center requires a public privacy-policy URL before this app can be
submitted to the Store (it reads Microsoft Graph presence/calendar data). That URL needs to exist
before submission can proceed at all, so it's the most time-sensitive piece of this work.

Both problems point at the same fix: a proper docs site, following the pattern already
established by the two sibling repos (`busybar-dotnet`, `psatool-busybar-agent`), each of which
has a `website/` Docusaurus site deployed to Cloudflare Pages. Once that site exists, the README
can point to it instead of duplicating its content — and while doing that pass, the other two
repos' READMEs get the same treatment where they duplicate their own existing docs sites.

## Scope

In scope:
- A new `website/` Docusaurus site under `oncall-busybar-agent`, mirroring
  `psatool-busybar-agent/website/`'s structure (not `busybar-dotnet/website/`'s, which carries an
  auto-generated API-reference sidebar this project has no equivalent of).
- A privacy policy page on that site (the urgent piece).
- Migrating the rest of the current README's substantive content into further doc pages on that
  same site, so the README can be stripped without losing information.
- A `deploy-docs.yml` GitHub Actions workflow for the new site, copied from
  `psatool-busybar-agent`'s, deploying to a new Cloudflare Pages project (already created by the
  user, along with the custom domain — see Deployment below).
- Updating the `url` field in all three repos' `docusaurus.config.ts` to the new subdomain scheme:
  `oncall.busy.homotechsual.dev`, `dotnet.busy.homotechsual.dev`,
  `psatool.busy.homotechsual.dev`.
- Stripping duplication out of all three repos' READMEs in favor of linking to their docs sites,
  to the degree each repo's existing content actually duplicates its docs site (see per-repo
  detail below — this differs per repo, it is not a uniform trim).

Out of scope:
- Provisioning Cloudflare Pages projects or custom domains, or the `CLOUDFLARE_ACCOUNT_ID` /
  `CLOUDFLARE_API_TOKEN` repo secrets — already done / being done by the user.
- Actual Microsoft Store submission (Partner Center account, listing content, screenshots) —
  this work only makes the privacy-policy URL exist; submission itself stays a manual step.
- Any content beyond what's already in each repo's current README — no new features are being
  documented, this is a reorganization plus one genuinely new page (the privacy policy).
- Task 5 Step 8 of the MSIX packaging plan (end-to-end ARM64 verification) — unrelated, still
  open, tracked separately.

## Site structure (oncall-busybar-agent/website/)

Copy `psatool-busybar-agent/website/`'s scaffold as the starting point: Docusaurus 3.10.2,
`@docusaurus/preset-classic`, yarn + Corepack, TypeScript config, no blog, a single `docsSidebar`
(no `apiSidebar` — that's specific to `busybar-dotnet`'s generated API reference, which has no
equivalent here), same homotechsual palette `custom.css`, same footer/GitHub-link/copyright
markup.

`docusaurus.config.ts` differences from psatool's:
- `title`: `"BUSY Bar on call agent"`
- `tagline`: `"Keeps a physical BUSY Bar's timer in sync with your Microsoft Teams on-call status"`
- `url`: `https://oncall.busy.homotechsual.dev`
- `organizationName`: `homotechsual`, `projectName`: `oncall-busybar-agent`
- `editUrl`: `https://github.com/homotechsual/oncall-busybar-agent/tree/main/website/`
- Footer links point at this project's own doc pages (see below) instead of psatool's.

### Doc pages

Each page below is substantially a reorganized move of existing README content into its own
page, not new writing (except Privacy, which is new). `sidebars.ts`'s `docsSidebar` order matches
this list:

1. **`intro.mdx`** (slug `/`) — what the app does (the README's opening two paragraphs: Graph
   presence as primary signal, calendar for timer length, mic fallback), then the "Sign in" and
   "Optional: register your own Entra app" sections moved here in full.
2. **`configuration.mdx`** — the four-layer config precedence explanation and the full settings
   table, moved from the README's "Configure" section.
3. **`intune-group-policy.mdx`** — the "Managing settings via Intune or Group Policy" section
   moved from the README, plus the content currently in `admx/README.md` folded in (import
   steps, Group Policy steps, the namespace-versioning update caveat, the "what this does not
   cover" note). `admx/README.md` becomes a short pointer at this doc page instead of duplicating
   it, the same way the main README will.
4. **`running.mdx`** — the "Run (production)" section moved from the README: the self-contained
   publish command, the apostrophe-in-path `MSB3094` troubleshooting note, the Startup-folder
   toggle explanation, and the code-signing / Store-signing note.
5. **`privacy.mdx`** — new content (see below).
6. **`development.mdx`** — the "Development" section's build/test/run commands, moved as-is.

### Privacy policy content (`privacy.mdx`)

Grounded in what the app actually does (confirmed via this session's own work on it, and a grep
of `src/` turning up no telemetry/analytics/crash-reporting code):

- **What data is accessed**: Microsoft Graph delegated permissions `Presence.Read` (to detect
  on-call status) and `Calendars.Read` (to size the timer to the current meeting's remaining
  time), consented by the signed-in user at sign-in time. Local microphone activity (on/off only,
  not audio content or recordings) as a fallback signal when Graph presence misses a call.
- **Where it goes**: nowhere beyond the two systems the app is designed to talk to — Microsoft
  Graph (via Microsoft's own sign-in and API, under the user's own tenant's data-handling terms)
  and the BUSY Bar device on the local network. No telemetry, analytics, or crash reporting of any
  kind is built into the app.
- **What's stored locally**: the MSAL authentication token cache (managed by Microsoft's
  authentication library, not this app's own code) and the per-user `appsettings.json` — both
  under `%LOCALAPPDATA%`, both local to the signed-in Windows user, never transmitted anywhere by
  this app.
- **IT-managed deployments**: organizations that deploy this via Intune/Group Policy may centrally
  set (not read back) configuration values through the registry policy layer described in
  [Intune / Group Policy](/intune-group-policy) — this doesn't change what data the app itself
  accesses or where it goes.
- **Contact**: a line pointing at the GitHub repo's issues page for questions, since that's this
  project's existing support channel.

## Deploy workflow

Copy `psatool-busybar-agent/.github/workflows/deploy-docs.yml` verbatim, changing only
`--project-name=oncall-busybar-agent` in the `wrangler-action` command step. No API-doc-generation
step is needed (that's `busybar-dotnet`-specific). The Cloudflare Pages project and the
`oncall.busy.homotechsual.dev` custom domain already exist (done by the user); the
`CLOUDFLARE_ACCOUNT_ID` / `CLOUDFLARE_API_TOKEN` repo secrets are being added by the user
separately.

## Cross-repo URL updates

Edit only the `url` field in each of the other two repos' `docusaurus.config.ts`:
- `busybar-dotnet/website/docusaurus.config.ts`: `url: 'https://dotnet.busy.homotechsual.dev'`
- `psatool-busybar-agent/website/docusaurus.config.ts`: `url: 'https://psatool.busy.homotechsual.dev'`

No other changes to either repo's Docusaurus config or deploy workflow — Cloudflare Pages project
names stay as they are (`busybar-dotnet`, `psatool-busybar-agent`); only the custom domain the
project is reached at changes, and that's already done on the Cloudflare side.

## README changes, per repo

Each repo's README is trimmed only where it duplicates content that now has a canonical home in
that repo's own docs site — the degree of trimming differs because the degree of existing
duplication differs.

### oncall-busybar-agent (heaviest trim)

Nearly everything below "## Sign in" currently duplicates a doc page created above. New README
shape:
- Title + one-paragraph description (kept as-is).
- A "Documentation" section near the top linking to the docs site's Getting Started, replacing
  "Sign in" / "Optional: register your own Entra app" / "Configure" /
  "Managing settings via Intune or Group Policy" / "Run (production)" entirely.
- "Development" section's command blocks stay (they're repo-local dev commands, the same content
  now also on `/development`, but conventional to keep in a README for anyone cloning the repo
  without opening a browser).
- No License section currently exists in this README; this pass doesn't add one.

### psatool-busybar-agent (moderate trim)

Its docs site already has `configuration.mdx`, `deployment.mdx`, `providers.mdx` covering the
same ground as the README's configuration table, Least Privilege section, Gorelo provider notes,
and Docker networking section. Replace those four sections with links to their doc-page
equivalents. Keep:
- The top one-paragraph description.
- "Development" (repo-local build/test commands).
- "Deployment" 's quick command block — the doc page's version has the fuller Docker-networking
  troubleshooting detail; the README keeps just the minimal `cp .env.example .env` +
  `docker compose up -d --build` snippet, same rationale as oncall's Development section above.

### busybar-dotnet (light trim, NuGet constraint)

This README renders as the NuGet package page (confirmed: nuget.org displays a package's
README directly), so Install and Quick start stay in place even though they're duplicated
(near-verbatim) in `docs/intro.mdx` — removing them would leave the NuGet page without any code
sample. Changes here are narrower:
- Add a docs-site link (Getting Started) near the badges.
- The "Scope" section's exclusion detail (WebSocket streaming / WebGL rendering out of scope)
  currently exists only in the README, not in `docs/intro.mdx` — copy it into `intro.mdx` first,
  then the README's own "Scope" section can shrink to a one-line pointer at the docs page instead
  of restating the exclusion list.
- "Error handling" section: replace with a link to the docs page's equivalent section (which
  already exists there, linking to the API reference type).
- "Development transparency" and "License" stay untouched — neither is documented on the docs
  site, and both belong on a README regardless.

## Testing / verification

- `yarn build` succeeds in `oncall-busybar-agent/website/` (new site).
- `yarn build` still succeeds in the other two repos' `website/` directories after the `url` field
  edit (a config-only change, but cheap to verify it didn't break anything).
- Every link the stripped READMEs point at resolves to a real page on the corresponding docs site
  (manually cross-check each link against the sidebar list above / the existing sibling sites'
  structure).
- No content is lost: every section removed from a README exists verbatim (or as the basis for
  new content, for the privacy policy) on the corresponding docs page.

## Out of scope reminders

This does not touch: Cloudflare project/domain/secret provisioning (user-owned), Microsoft Store
submission itself, or the still-open MSIX Task 5 Step 8 (ARM64 sign-in verification) — none of
those are affected by or blocking on this work.
