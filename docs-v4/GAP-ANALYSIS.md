# FlexQuery.NET v4 Documentation Gap Analysis

Internal quality report for the v4 documentation effort. Produced after the v4 documentation
was authored from HEAD source, with legacy `docs/` consulted afterwards as evidence only.

Sources kept separate throughout:

- **A** — what v3.1.1 actually implemented (verified via `git show v3.1.1:...`)
- **B** — what the v3-era documentation documented (legacy `docs/` VitePress site)
- **C** — what current HEAD implements

---

## 1. Undocumented in v3.1.1 (existed in A, absent from B)

Verified against v3.1.1 source:

- **Dapper conventions and relationship queries** — v3.1.1 shipped `DefaultEntityConvention`,
  `DefaultForeignKeyConvention`, `DefaultRelationshipConvention`, `DefaultPluralizer`, but the
  legacy docs only covered explicit configuration (`providers/dapper/*` covered dialects and
  SQL generation, not the convention defaults). → Now documented under
  [Dapper](content/docs/providers/dapper/index.mdx).
- **`FieldRegistry` static registration** — public security API (`Register<T>` /
  `IsAllowed`) with no page in the legacy docs (only governance options were documented).
  → Now documented under [Security](content/docs/security/index.mdx).
- **`AllowOperators` per-field operator allow-lists** — present in v3.1.1 governance code,
  undocumented. → Now documented under [Security](content/docs/security/index.mdx).
- **Grouped-query contract details** (HAVING-before-paging ordering, count-of-groups
  semantics) — partially covered in `docs/architecture/grouped-query-contract.md`, but never
  surfaced in the guide. → Now documented under
  [Grouping & Aggregates](content/docs/guides/grouping.mdx) and
  [Execution Pipeline](content/docs/concepts/pipeline.mdx).

## 2. Partially documented in v3.1.1

- **Filtering operators** — the legacy `shared/operators.md` and `guide/filtering.md`
  listed operators but omitted alias normalization (`==`, `sw`, `cn`, …), collection-operator
  syntax details, and the `like` EF Core-specific handler. → Now covered in
  [Filtering](content/docs/guides/filtering.mdx).
- **Paging** — legacy `guide/paging.md` covered `page`/`pageSize` but not clamping rules
  (1–1000), `includeCount=false` count-skip behavior, or `DisablePaging` variants. → Now
  covered in [Paging](content/docs/guides/paging.mdx).
- **Configuration** — legacy docs described DI-based configuration that only partially
  matched the v3.1.1 surface (see §3). Provider-level configuration was spread across
  pages without a precedence model. → Now covered in
  [Configuration](content/docs/concepts/configuration.mdx).

## 3. Incorrectly documented in v3.1.1 (B contradicted A or C)

- **`MapField` as the DTO strategy** — `guide/dto-mapping.md` presented `MapField` as "the"
  DTO mapping solution with no limitations; v4 replaces this story with the type-map model
  (`CreateMap`/`ForMember`/`ForNavigation`). The v4 docs document the current model
  ([Typed DTO Projection](content/docs/guides/typed-dto-projection.mdx)); `MapField` remains
  for alias-expression mapping but is no longer the DTO story.
- **Dapper manual dialect configuration** — v3.1.1 `providers/dapper/dialects.md` documented
  manual `Dialect` selection; the v4 implementation auto-detects from the connection and the
  manual API was removed. Documented in [Dapper](content/docs/providers/dapper/index.mdx)
  and the migration guide.
- **JQL naming** — the legacy docs describe "JQL (Jira Query Language)"; v4 renames the
  language and package to FQL (FlexQuery Language). All v4 docs use FQL.
- **Aggregate syntax in select** — legacy grouping docs used `select=sum(Total)` style;
  v4 moved aggregates to a dedicated `aggregate` parameter with `function:field` grammar.
  Documented in [Query Syntax](content/docs/concepts/query-syntax.mdx) and
  [Grouping & Aggregates](content/docs/guides/grouping.mdx).

## 4. Documented but missing detail (examples, config, limitations, edge cases)

- **CaseInsensitive behavior** — legacy docs claimed "no performance penalty on most
  providers"; the option was removed entirely in v4. Behavior differences are now covered by
  the migration guide's removed-APIs section.
- **Filtered includes semantics** — `guide/include-filtering.md` documented the concept but
  not syntax edge cases (duplicate paths, empty option lists), provider hydration strategy
  (split queries), or count-query interaction. The v4 [Expand](content/docs/guides/expand.mdx)
  page covers validation rules and provider behavior explicitly.
- **Security** — `guide/security.md` and `security-governance.md` duplicated overlapping
  content without a threat model or operator/role interplay. The v4
  [Security](content/docs/security/index.mdx) page consolidates with a defense-in-depth
  checklist.
- **Validation** — `guide/validation.md` listed exception types but not the rule families or
  the structured `ValidationError` shape. →
  [Validation](content/docs/guides/validation.mdx).

## 5. Added v3.1.1 → HEAD (require new documentation)

All documented; verification trail in the change matrix:

| Feature | Page |
|---|---|
| Typed DTO `FlexQueryAsync<TEntity,TResponse>` (EF Core + Dapper) | Typed DTO Projection |
| AutoMapper-style mapping + global registry | Typed DTO Projection |
| `expand` trees | Expand |
| Keyset pagination (`cursor`, `NextCursorToken`, `SeekAfter`) | Keyset Pagination |
| `Query.Create()` fluent API | Fluent API |
| OpenAPI package | OpenAPI |
| Governance per-operation sets + roles | Security |
| Result shaping (`ResultShape` JSON converter) | Query Result, ASP.NET Core |
| Dapper `ModelBuilder` + SQL logging | Dapper |
| Rule-based validation | Validation |
| `select` aliases + space-form sort | Projection, Sorting |
| CancellationToken across async overloads | EF Core, Dapper |

## 6. Behavior-changed v3.1.1 → HEAD

Documented in the v4 guides (current behavior) and the migration guide (historical delta):
DSL `AND`/`OR` keywords, strict paging validation, immutable global config,
navigation-only includes, HAVING declared-aggregate enforcement, COUNT collection-only sort
targets, aggregate alias PascalCase default, expand duplicate-path rejection.

## 7. Removed / renamed v3.1.1 → HEAD

Documented in [Migrate from v3](content/docs/migration/v3-to-v4.mdx) with replacements:
JQL→FQL package rename, `FlexQueryConfiguration`→`FlexQueryCore`, `SortOption`→`SortNode`,
`SelectModel`→`SelectNode`, `AggregateModel`→`Aggregate`, `FilteredIncludes`→`Expand`,
JSON/Indexed/Generic syntax removal, `CaseInsensitive` removal, DI parser registration
removal, unified exception hierarchy.

Legacy doc pages that reference removed behavior (now obsolete for v4):
`guide/include-filtering.md`, `guide/dto-mapping.md` (as DTO story),
`providers/dapper/dialects.md`, `guide/query-formats.md` (JSON/Indexed/Generic sections),
`shared/query-language.md` (JQL naming), `guide/query-composition.md`,
`guide/flattening.md` (content folded into Projection), `docs/v1/**` (historical archive).

## 8. Obsolete legacy documentation

The entire legacy `docs/` site describes the v3 DI-era API. Pages whose *core subject*
no longer exists in v4: `include-filtering.md`, `dialects.md`, `query-formats.md`,
`field-mapping.md` (as primary DTO mechanism), all `v1/**` pages, and the JQL references
throughout. The v4 site replaces all of these.

## 9. Resolved gaps

Every gap in §1–§4 is closed by the new v4 documentation:

| Gap | Where resolved |
|---|---|
| Dapper conventions undocumented | providers/dapper |
| FieldRegistry / AllowOperators undocumented | security |
| Operator alias normalization undocumented | guides/filtering |
| Paging clamping / count-skip undocumented | guides/paging |
| Configuration precedence undocumented | concepts/configuration |
| DTO mapping story outdated | guides/typed-dto-projection |
| Dialect auto-detection undocumented | providers/dapper + migration |
| FQL rename undocumented | concepts/query-syntax + migration |
| Aggregate parameter syntax undocumented | concepts/query-syntax + guides/grouping |
| Expand rules/limits undocumented | guides/expand |
| Validation rule families undocumented | guides/validation |
| All v4 features (§5) | see table in §5 |
| All behavior changes (§6) | migration/v3-to-v4 + change matrix |
| All removals/renames (§7) | migration/v3-to-v4 |

**Verification note:** classification of "existed in v3.1.1" was confirmed via
`git show v3.1.1:<path>` and `git grep` against the tag — e.g. keyset pagination, expand,
and the `Query.Create()` fluent API do **not** exist anywhere in the v3.1.1 tree, while
`FilteredIncludes`, `JsonQueryParser`, and the aggregate-in-select grammar do.
