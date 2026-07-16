# Public API Review Checklist — Koras.Results

Apply to every PR that changes `PublicAPI.Unshipped.txt` (i.e., any public-surface change). The reviewer answers every line.

## Necessity
- [ ] Does this member solve a documented user problem (feature-catalog reference)?
- [ ] Could it be `internal`? (Default answer should be yes — justify public.)
- [ ] Does an existing member already cover the scenario?

## Shape
- [ ] Name follows [naming-guidelines.md](naming-guidelines.md) vocabulary exactly?
- [ ] Namespace = package ID?
- [ ] Async methods: `Async` suffix, `Task` return, `ConfigureAwait(false)` inside, CancellationToken last-and-optional where the API owns I/O?
- [ ] No boolean parameters where an enum/overload is clearer?
- [ ] No more than 4 parameters (else options object)?
- [ ] Generic arity minimal; constraints necessary and sufficient?
- [ ] Nullability annotations exact (`[MaybeNullWhen]`, `[MemberNotNullWhen]` where applicable)?

## Safety
- [ ] Argument guards throw `ArgumentNullException`/`ArgumentException` eagerly (not deferred into iterators/tasks)?
- [ ] Failure-access guards throw `InvalidOperationException` with actionable messages?
- [ ] No hidden I/O, no logging, no static mutable state?
- [ ] Thread-safety documented if the type is a service?
- [ ] No third-party type in a core-package signature?

## Compatibility
- [ ] Additive only within the current major ([backward-compatibility.md](backward-compatibility.md))?
- [ ] New overloads checked against existing call sites for resolution changes (test added)?
- [ ] `PublicAPI.Unshipped.txt` updated in the same commit; no `Shipped.txt` edits outside release PRs?
- [ ] Serialization shape unchanged, or change flagged as major + snapshot tests updated deliberately?

## Quality
- [ ] XML docs on every new public member (`<summary>`, `<param>`, `<returns>`, `<exception>`)?
- [ ] Unit tests: happy path, guard paths, and semantic contract (e.g., short-circuiting)?
- [ ] Feature guide / concepts doc updated?
- [ ] CHANGELOG entry under `[Unreleased]`?
- [ ] Sample updated if the feature is user-visible?
