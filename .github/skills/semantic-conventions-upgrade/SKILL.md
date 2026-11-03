---
name: semantic-conventions-upgrade
description: Upgrade the OpenTelemetry semantic conventions version that a component in opentelemetry-dotnet-contrib targets (or bump the generated SemanticConventions package). Use when asked to update, bump, migrate or adopt a new semantic conventions version, change a schema URL, or align an instrumentation or resource detector with newer semantic conventions.
---

# Semantic Conventions Upgrade

Use this skill when moving a component under `src/` from one version of the
[OpenTelemetry Semantic Conventions](https://github.com/open-telemetry/semantic-conventions)
to another, or when bumping the generated
`OpenTelemetry.SemanticConventions` package. It is also a good checklist for
any change that alters the attributes, metrics, span names or span kinds a
component emits.

The goal is a change that is correct the first time. Most mistakes in past
upgrades came from the same few causes, which are called out as **Lessons**
throughout and summarised in the final checklist.

## Core principle: the declared version is a claim

Every instrumentation scope created via `ActivitySourceFactory` or
`MeterFactory`, and every `Resource` created with a schema URL, declares a
semantic conventions version. That version becomes the telemetry schema URL
(`https://opentelemetry.io/schemas/<version>`) which tells backends how to
interpret the data.

> [!IMPORTANT]
> Only declare a version when what the component emits actually matches that
> version. Bumping the constant is the *last* step, after the behavior has been
> verified or changed, never a first step and never a no-op.

Per `REVIEW.md`, the instrumentation scope must carry both a `Version` and a
`SchemaUrl`. When the supported semantic convention version changes, update the
`SchemaUrl` (via the declared constant); the scope `Version` stays the
package's own version.

## Step 0: Classify the change

Decide which of these you are doing; they have different rules and often
belong in **separate PRs** (one PR per concern, one CHANGELOG entry per
affected component).

| Kind | Example | Notes |
| --- | --- | --- |
| A. Regenerate `OpenTelemetry.SemanticConventions` | `[SemanticConventions] Bump to v1.45.0` | Mechanical; see [Generated package](#generated-package-opentelemetrysemanticconventions). Land first. |
| B. Declared-version bump only | `[Repo] Update declared semantic conventions version` | Only for components where nothing emitted changes. Needs evidence (Step 2). |
| C. Behavioral migration | ConfluentKafka, WCF, gRPC, Elasticsearch, Redis | Attributes, names, kinds, metrics change. Needs a compatibility strategy (Step 4). |
| D. New selectable version | AWS `SemanticConventionVersion.V1_45_0` | Adds an option; the default must not change. |

Do not mix kinds A, B and C in a single PR unless the user asks. Components that
need design decisions should never be held up by, or hidden inside, a PR that
bumps unrelated components.

## Step 1: Establish the starting point

1. **Find every declared version in the component.** Do not assume there is
   one.

   ```sh
   grep -rnE "SemanticConventionsVersion|new\(1, [0-9]+, 0\)|new Version\(1, [0-9]+" src/OpenTelemetry.<Type>.<Name>
   grep -rnE "semantic-conventions/(blob|tree)/v[0-9.]+" src/OpenTelemetry.<Type>.<Name>
   ```

   Components can have several: separate constants for traces and metrics
   (Owin), different constants for the old and new opt-in paths
   (EntityFrameworkCore: old `1.24.0`, new `1.36.0` before the bump),
   per-TFM implementations (Http: `HttpWebRequest` on .NET Framework vs
   `DiagnosticSource` handlers), shared constants referenced across files
   (Kusto: one constant shared by `ActivitySource` and `Meter` so they cannot
   drift), and resource detectors that pass the version to `SchemaUrls.Get`.
   Every one must be accounted for and deliberately kept or changed.
2. **Find what the README claims.** README links to
   `semantic-conventions/blob/vX.Y.Z/...` are frequently stale or already
   disagree with the code (for example the Http README linked `v1.40.0` while
   the code declared `1.41.0`). Treat them as documentation to update, never
   as evidence of what the code does.
3. **Record the range.** The upgrade is from *current declared version* to
   *target version*, e.g. `1.41.0 -> 1.45.0`. List every released tag in
   between (`1.42.0`, `1.43.0`, `1.44.0`, `1.45.0`).

> [!NOTE]
> **Lesson: the range matters, not just the target.** A component moving from
> `1.41.0` to `1.45.0` must account for the changes in `1.42.0`, `1.43.0`,
> `1.44.0` *and* `1.45.0`. The release notes and schema file of the target
> version describe only that step. A rename made in `1.43.0` that the
> component never adopted is still owed by the time it declares `1.45.0`, and
> a change introduced in `1.42.0` and partially reverted in `1.44.0` must be
> judged on the net result. Never review only the latest version's diff.

## Step 2: Gather the changes for every version in the range

For **each** version in the range, collect changes from all of these sources.
Using only one source misses things.

1. **Schema file.** `schemas/<version>` in the semantic-conventions repo is the
   authoritative list of attribute and metric *renames* per version. Read every
   file in the range, not only the target.
2. **Release notes and changelog** for each tag
   (`https://github.com/open-telemetry/semantic-conventions/releases/tag/vX.Y.Z`).
   Scan for the signals the component emits (HTTP, DB, RPC, messaging,
   FaaS, cloud, host, process, runtime, etc.), including deprecations,
   stability changes and requirement-level changes.
3. **Resolved registry diff.** Resolve the registry at the *current* and
   *target* tags with Weaver and diff the output. This catches changes the
   schema file does not: type changes, requirement level changes, new
   attributes, enum member changes, units, span kinds, instrument types.

   ```sh
   git clone --depth 1 --branch v1.45.0 https://github.com/open-telemetry/semantic-conventions sc-1.45.0
   docker run --rm -v "$PWD/sc-1.45.0/model:/source" otel/weaver:v0.28.0 \
     registry resolve --registry /source --format json > resolved-1.45.0.json
   ```

   Use the Weaver version that `test/Shared/weaver.Dockerfile` pins, or newer if
   the target registry requires it (v1.45.0 needed v0.28.0 for `unref`). Run
   it against the starting tag too and diff the two JSON files, filtered to the
   relevant groups.
4. **The docs themselves.** Weaver's resolved v1 JSON does **not** show v2
   refinement spans, so a span's name format, span kind, sampling-relevant
   attributes and conditional requirement text can only be seen by diffing the
   markdown under `docs/<area>/` between the two tags. Do this for every
   page the component implements (for example `docs/db/database-spans.md`,
   `docs/db/database-metrics.md`, `docs/messaging/kafka.md`).
5. **Dependent conventions.** Common attributes (`server.*`, `network.*`,
   `error.type`, `url.*`) and per-technology pages (`docs/db/sql-server.md`,
   `docs/db/redis.md`, ...) can change independently of the area page.

Produce a **table per version** (this goes in the PR description or a scratch
file, and the final PR should summarise it):

| Version | Change | Affects this component? | Evidence / decision |
| --- | --- | --- | --- |
| 1.42.0 | `rpc.method` now `service/method` | Yes | Qualified name emitted; tests updated |
| 1.43.0 | (nothing relevant) | No | Checked schema, release notes, docs diff |
| 1.44.0 | ... | ... | ... |

Every version in the range must have at least one row, even if the row says
"nothing relevant, checked X, Y and Z". An empty cell means the version was not
reviewed.

### Categories of change to look for

Do not stop at attribute renames. Previous upgrades turned up each of these:

- **Renames and removals:** `messaging.system` -> `messaging.system.name`,
  `messaging.kafka.*` -> `kafka.*`, `faas.invocation_id` ->
  `faas.invocation.id`, `net.peer.*` -> `server.*`.
- **Enum value changes:** the SQS system value `aws_sqs` -> `aws.sqs`.
- **Type changes:** an `int` attribute becoming a `string` (Kafka
  `destination.partition.id`, RPC `rpc.response.status_code`). Per
  `REVIEW.md`, attribute values must match the specified type.
- **Requirement-level changes:** attributes becoming `Required`,
  `Conditionally Required` or `Opt-In`; conditions changing; values that must
  now be omitted (Kusto: `server.port` is not recorded when it is the default
  port for the scheme).
- **Span name format:** `send {topic}`, `{operation} {target}`, low-cardinality
  rules.
- **Span kind:** the Kafka *poll* span is `Client`, not `Consumer`; AWS
  single-message `SendMessage`/`Publish` became `Producer` while batch
  operations stay `Client`.
- **Status and error handling:** `error.type` values and when the span status
  is set, and what goes in the status description.
- **Metric changes:** renamed or merged instruments (Kafka publish/receive
  duration collapsing into one `messaging.client.operation.duration`), units,
  instrument type, required attributes and bucket advice.
- **Semantics of an unchanged attribute:** `server.address` in the database
  conventions changed meaning (the configured address, with the contacted node
  moving to `network.peer.*`), which an unchanged attribute name would hide.
- **Newly required or recommended attributes** the component can now provide.
- **Deprecations** of attributes the component emits. Deprecation alone does
  not make an attribute invalid at the target version: check the target
  area's requirement level and migration notice. For example, the AWS SDK
  span conventions at v1.45.0 still mark the deprecated `rpc.system` as
  `Required` and `rpc.service` as `Recommended`. Replace an attribute only
  when the area's conventions no longer require or recommend it, or the notice
  says to; otherwise keep it and note the decision in the per-version table.

## Step 3: Inventory what the component actually emits

You cannot judge a change without knowing the current output.

- Grep for attribute constants and literals: the generated `Attribute*`
  classes, the hand-written `SemanticConventions.cs` constants in `src/Shared`
  and per-component files, and raw `"db."`/`"rpc."`/`"messaging."` strings.
- List span names, span kinds, status mapping, metric names, units, instrument
  types and their tag sets.
- Check the **timing** of attributes. Sampling-sensitive attributes must be set
  before the activity starts (`REVIEW.md`).
- Check every TFM. `#if NETFRAMEWORK`/`#if NET` code paths can often write the same
  attributes in different places (WCF client vs server, Http handlers vs
  `HttpWebRequest`).
- **Check for native instrumentation.** On newer runtimes parts of the
  telemetry are emitted by the framework itself (HttpClient on .NET 9+,
  ASP.NET Core). The library must not change or override native attributes,
  and the framework's version of the conventions governs them. Make sure that
  claiming a newer version is honest for what the library itself emits, and
  that the sibling/library-created activities carry the same attributes as the
  native ones.
- Read the README attribute/metric tables; they are part of the contract and
  must be updated together with the code.

> [!NOTE]
> **Lesson: look at how the library gets its data before promising an
> attribute.** The StackExchange.Redis `server.address` change needed a design
> decision because the library only knows the contacted node, not the
> configured address. ElasticsearchClient could not honestly emit
> `db.operation.name` because NEST does not expose the endpoint identifier.
> The SqlClient payload has no row count except through the statistics
> dictionary. If the data is not available, **omit the attribute and say so**
> rather than guess or derive it heuristically, and confirm with the user.

## Step 4: Decide what to do about each change

For each row in the Step 2 tables choose one of:

1. **Apply** - emit the new shape.
2. **Not applicable** - with evidence (the component does not emit it).
3. **Defer** - with a reason, and exclude the component from the version bump
   (see below).

If a design question has more than one reasonable answer (default behavior,
omit vs. approximate, which attribute carries which address), ask the user
instead of deciding silently.

### Compatibility strategy for behavior changes

Changing emitted telemetry is a breaking change for dashboards, alerts and
sampling rules. Choose the strategy from the migration guidance for the
convention area and the component's existing compatibility contract, not from
package stability alone.

Each area's notice only applies to components that emit conventions at or
before a starting version, and that version differs per area. Read it from the
notice at the target tag instead of assuming one. At v1.45.0:

| Area | Opt-in values | Notice applies to components emitting |
| --- | --- | --- |
| Database | `database`, `database/dup` | v1.24.0 or prior |
| RPC | `rpc`, `rpc/dup` | v1.37.0 or prior |
| Messaging | `messaging`, `messaging/dup` | v1.24.0 or prior |

1. **Read the area's migration notice** at the target tag (for example
   `docs/db/README.md`, `docs/rpc/README.md`). Check whether it defines an
   `OTEL_SEMCONV_STABILITY_OPT_IN` value, what the default must be, and when
   the variable applies (the RPC notice limits it to the migration from
   experimental conventions to their initial stable version).
2. **Read the component's existing contract:** its README, CHANGELOG, options
   and any helper it already uses. Keep opt-ins it already ships; do not
   remove or reinterpret them.
3. Then pick the matching case:
   - **Experimental to initial stable, where the notice defines opt-in values
     and the component is at or before the notice's starting version:** keep
     the default output and use the dual-emit pattern.
     - Database: `DatabaseSemanticConventionHelper`.
     - RPC: `RpcSemanticConventionHelper`.
     - Messaging: no shared helper exists yet; model one on the two above.
     - Link the shared helper (`src/Shared`) rather than copying it, and add
       the option properties (`EmitOldAttributes`/`EmitNewAttributes` style)
       that the other libraries use.
     - Create **separate versioned scopes**: old (previous version), new
       (target version) and "both" (schema URL `null`, because the output
       matches no single schema), e.g. `ActivitySourceFactory.Create<T>(null)`.
       Pick the scope per mode.
     - In `dup` mode, a key that exists in both shapes with conflicting values
       (RPC's `rpc.method`) needs a documented winner. Prefer the new value
       and document the compromise.
     - Only bump the declared version of the **new** path when the library is
       in old-by-default mode; the old path's version stays put
       (EntityFrameworkCore bumped only the database opt-in path).
   - **The component already ships an opt-in contract** (stable or
     pre-release): extend that contract. Do not replace it with a hard
     cutover.
   - **The component is past the notice's starting version, or the area
     defines no opt-in value:** the notice does not apply, so do not invent a
     new opt-in variable. Follow the notice. A pre-release package with no
     existing contract may do a hard cutover; mark it `**Breaking change**` in
     the CHANGELOG, describe every rename, and confirm the approach with the
     user.
   - **The area is already stable** (for example HTTP): no new opt-in; follow
     the notice for any remaining migration.
   - **New selectable version enums (AWS):** add the new member (`V1_45_0`),
     make `Latest` resolve to it, update the public API files, and **do not
     change the default**. Derive the new conventions class from the previous
     one and override only what changed.

### Components to leave out of a bump

If any item in the range is unmet or deferred, the component's declared version
must **not** be bumped. State this explicitly in the PR ("Intentionally not
included") with the reason, for example:

- StackExchangeRedis because the `server.address` guidance needs a design
  decision.
- A library that maps attributes it does not own (for example an exporter
  translating `messaging.system`), where the effect on consumers needs
  separate analysis.

## Step 5: Implement

1. **Version constants.** Update every constant found in Step 1, keeping
   shared constants shared. Verify with a grep afterwards that no stale `1.xx`
   remains unintentionally.
2. **Generated constants.** Prefer the generated attribute classes from
   `OpenTelemetry.SemanticConventions` when the component references that
   package. For components that use `src/Shared/SemanticConventions.cs` or
   per-component constants, rename the constants *and* the string values, and
   check every use.
3. **Emission changes.** Make each change from Step 4; keep old and new paths
   separate and well-commented so the old path can be deleted when it is
   retired. Where required attribute values depend on data that may not be
   present (topic, partition, endpoint), set them only when known.
4. **Package references.** Keep to the minimal OpenTelemetry package
   (`OpenTelemetry.Api` where possible) per `AGENTS.md`; add `src/Shared`
   files via linked `<Compile Include>` items and versions only in
   `Directory.Packages.props`.
5. **Public API.** New public members go into
   `.publicApi/PublicAPI.Unshipped.txt`.
6. **Recommended and Opt-in attributes.** Only add new Public API surface for
   attributes that are recommended or opt-in according to the semantic conventions
   when explicitly asked to do so by the user.
7. **Do not hand-edit generated files.**

### Gotchas to check for in the implementation

- **Const value collisions.** Generated constant names come from pascal-casing
  the attribute id. Two different ids can produce the same name, and the
  surviving *value* may silently change. In 1.45.0
  `AttributeFaasInvocationId` changed from `faas.invocation_id` to
  `faas.invocation.id` because both pascal-case to the same name. After
  regenerating, diff the `.cs` files and the `PublicAPI.Unshipped.txt` for
  *value* changes, not only added and removed members, and search consumers.
- **Shared metrics.** When several old instruments collapse into one new
  instrument, producers and consumers share it; do not assume one metric per
  operation in tests (ConfluentKafka produced 3 distinct metric names, not 4).
- **Distinct components, same attribute.** Components implemented over a shared
  helper (AWS, AWS Lambda and the AWS resource detectors share
  `src/Shared/AWS`) must all be updated and tested together.
- **Resource detectors** pass the version through `SchemaUrls.Get`. Check each
  detector's emitted resource attributes against the range as well; a detector
  may need only the bump (Host, Process, OperatingSystem) or an attribute
  change (Gcp `gcp.gce.instance.name`).
- **Netfx quirks.** `net462` is the minimum framework; check any new code
  paths there (nullable annotations, missing BCL APIs, `Activity` clock
  differences).
- **Culture.** Number-to-string conversions use `CultureInfo.InvariantCulture`
  (e.g. `((int)status).ToString(CultureInfo.InvariantCulture)` for string
  status codes).
- **Performance.** Where possible, avoid boxing value types when adding attributes
  to activities or metrics, as this can introduce unnecessary allocations due to
  needing to box value types to pass to the overload that accepts `object`.

## Step 6: Tests

1. **Schema URL assertions.** Tests that assert the schema URL must be updated
   to the constants (or the new literal) for each mode. For dual-emit, assert
   the schema URL for old, new and both modes. Do not change scope `Version`
   expectations: the factories set it from the package version, not the
   semantic conventions version.
2. **Attribute and metric assertions per mode.** For opt-in libraries,
   parameterise tests over the modes (Old/New/Dupe) rather than testing only
   the mode you changed. Assert the *absence* of attributes that must not be
   present in a given mode, including types (string vs int).
3. **Weaver conformance tests.** Components that have a
   `SpecificationTests : WeaverSpecificationTests` class validate their
   telemetry against the semantic conventions registry using Docker-based
   Weaver (`test/Shared/Weaver*.cs`). Pass the **declared version constant** (not
   a literal) to `AssertTelemetryConformsToSemanticConventions` so that the
   test validates the claim being made. If the component has no such test and
   emits telemetry covered by the conventions, add one modelled on
   `test/OpenTelemetry.Instrumentation.Http.Tests/SpecificationTests.cs`.
   - These tests are `[Trait("Category", "Weaver")]` and run on a Linux
     Docker engine. If Docker is unavailable locally, say so explicitly in the
     PR instead of reporting the suite as passing. The user may be able to start
     Docker manually for you to run the tests.
   - Keep `suppressAdvice` entries minimal, each linked to an upstream issue.
     Re-check existing suppressions when moving versions: a fixed upstream
     issue means the suppression should be removed.
   - Weaver fails *silently* (exit code 1, empty report) for some inputs: a
     span status without a string `message` property, or a registry that
     could not be downloaded. The shared fixture handles both (it always emits
     `message` and uses the `ansi` diagnostic format); do not switch the
     format back to `gh_workflow_command`, which hides non-policy errors.
     When a Weaver test fails with `Expected: 0 Actual: 1`, look at the
     Weaver output for download or parse errors first.
4. **Integration tests** (Docker or external services, such as Kafka and SQL
   Server) are gated by categories/traits; run them if possible and otherwise
   say they were not run.
5. **Native vs library.** On runtimes with native instrumentation, tests must
   not require the library to override native attributes.

## Step 7: Documentation and CHANGELOG

- **README:** update the `semantic-conventions/blob/v<version>/...` links that
  document what the component itself now emits, using tag permalinks (never
  `main`, per `REVIEW.md`). Classify each link first:
  - *Migrated path* (what the component emits at the new version): update to
    the new tag, including links that were already stale.
  - *Legacy path* (old default or old `OTEL_SEMCONV_STABILITY_OPT_IN` mode):
    keep the tag matching the version that path declares.
  - *Runtime- or framework-owned telemetry* (for example native HttpClient or
    ASP.NET Core instrumentation): link the version the runtime implements,
    not the version the library declares.
  - *Historical references* (migration notes, comparisons with older
    versions): leave unchanged.

  Update attribute and metric tables and any footnotes describing conditional
  behavior, and describe the opt-in environment variable values the library
  understands, where relevant.
- **CHANGELOG** in every affected component, under `## Unreleased`, new entries
  at the *end*, sentence case, ending in a period, with the PR link. Do not add
  a version header. Use the established wording for a plain bump:

  ```markdown
  * Updated Semantic Conventions version to `1.45.0`.
    ([#NNNN](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/pull/NNNN))
  ```

  For behavior changes, list each renamed/removed/retyped attribute or metric
  and prefix breaking changes with `**Breaking change**:`. For a dual-emit
  change, say which `OTEL_SEMCONV_STABILITY_OPT_IN` values enable it.
- **Generated package:** its CHANGELOG entry is
  `Updated to \`v1.45.0\` release of OpenTelemetry Semantic Conventions.`
- Every component touched gets its own entry. Components that were deliberately
  skipped get none.

## Step 8: Verify

Run in order and report outcomes faithfully, including anything not run:

```sh
dotnet build
dotnet test test/OpenTelemetry.<Type>.<Name>.Tests/ --no-build
dotnet format --verify-no-changes
markdownlint .
```

- Build in Release at least once so `TreatWarningsAsErrors` and the public API
  analyzers run; build all target frameworks including `net462` on Windows.
- Re-grep for stale versions in the component, its tests and its README.
- Re-read the diff for every constant whose *value* changed.
- Run the Weaver tests if Docker is available.
- Confirm no unrelated components were touched.

## Generated package (`OpenTelemetry.SemanticConventions`)

For kind A changes:

1. Update `SEMCONV_VERSION` (and `GENERATOR_VERSION` if needed) in **both**
   `src/OpenTelemetry.SemanticConventions/scripts/generate.sh` and
   `generate.ps1`. Keep them identical.
2. If the target registry uses features the pinned Weaver does not understand
   (for example `unref`), bump the generator and also update
   `test/Shared/weaver.Dockerfile` so the tests use a compatible version
   (pinned by digest; preserve that format).
3. Run the script (needs Docker) and review the full diff in `Attributes/`
   and `.publicApi/PublicAPI.Unshipped.txt`: additions, removals and
   *value changes* (see the const collision gotcha above).
4. Search the repository for usages of any removed or renamed member and fix
   them.
5. Add the CHANGELOG entry and keep the scripts' version consistent with
   whatever the README of the package claims.

## Pull request

- Follow `.github/PULL_REQUEST_TEMPLATE.md` and the AI disclosure rules in
  `AGENTS.md`.
- Describe, per version in the range, what was reviewed and the outcome.
  Reviewers should be able to see that `1.42.0`, `1.43.0`, ... were each
  considered, not only the last.
- List any components **intentionally not included** and why.
- Call out every observable behavior change and whether the default changes.
- When editing the description after it was first posted, **fetch the current
  body from GitHub** (`gh pr view N --json body -q .body`), apply a minimal
  targeted edit and push that; never overwrite it with a locally saved copy,
  because maintainers may have edited it (ticked boxes, struck-out items).
- Replies to PR comments and reviews are posted by the user, not by an agent.
- Do not push, open PRs or change repository visibility without the user's
  confirmation.

## Final checklist

Before declaring the work done, confirm each of these:

- [ ] All declared versions in the component were found (traces, metrics,
      old/new paths, per-TFM, resource schema URLs) and each was handled.
- [ ] Every version between the current and target was reviewed, using the
      schema file, release notes, resolved registry diff *and* docs diff.
- [ ] A per-version table of findings exists, including "nothing relevant"
      rows with evidence.
- [ ] Types, enum values, requirement levels, span names, span kinds, status
      and error mapping, and metric names/units/instruments were checked, not
      only attribute names.
- [ ] Data that cannot be sourced honestly is omitted, with the user's
      agreement, rather than guessed.
- [ ] The compatibility approach follows the area's migration notice and the
      component's existing opt-in contract (no new opt-ins invented, none
      dropped); any hard cutover is limited to pre-release packages without
      such a contract and is documented as breaking.
- [ ] The declared version is only bumped where the behavior matches; skipped
      components are listed with reasons.
- [ ] Generated constant *values* were diffed, not just names.
- [ ] Tests cover each opt-in mode, schema URLs, attribute types and absence of
      attributes; Weaver tests use the declared version constant.
- [ ] README links for the migrated path and tables, CHANGELOG entries (every
      affected component, at the end of `Unreleased`) and public API files are
      updated; legacy, runtime-owned and historical links were left alone.
- [ ] Build (all TFMs), tests, `dotnet format` and `markdownlint` outcomes are
      reported, with anything not run called out.
- [ ] PR description follows the template, discloses AI usage, and lists the
      components deliberately excluded.
