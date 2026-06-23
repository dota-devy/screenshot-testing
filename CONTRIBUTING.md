# Contributing

Thanks for your interest in improving **Surfshack.Screenshots.Testing**. This is a small,
focused library, and contributions — bug reports, docs fixes, features — are welcome.

## Where development happens

The authoritative repository is on **GitLab**:
<https://gitlab.com/surfshack/screenshot-testing-oss>

The GitHub repository is a **read-only mirror**. Pull requests opened against it can't be merged
(the mirror overwrites its branches on every sync), so please **open issues and merge requests on
GitLab** instead. If GitLab sign-up is a blocker for you, open a GitHub issue and we'll pick it
up — but code changes ultimately land on GitLab.

## Reporting bugs

A good bug report includes:

- What you did, what you expected, and what actually happened.
- A minimal repro if you can — ideally a small failing test against one of the samples.
- Versions: the `Surfshack.Screenshots.Testing` version, `Microsoft.Playwright` version, .NET SDK
  version, and OS.

For anything security-sensitive, please open a **confidential issue** on GitLab rather than a
public one.

## Development setup

**Requirements**

- .NET 10 SDK
- A Chromium browser for Playwright (the [Playwright .NET](https://playwright.dev/dotnet/) CLI
  installs one, or use a pre-baked Playwright image — see [`.gitlab-ci.yml`](.gitlab-ci.yml))
- For the DB-backed end-to-end tests, a database reachable from the test process (the sample uses
  SQLite; real consumers use Postgres, etc.)

**Build**

```bash
dotnet build Surfshack.Screenshots.Testing.slnx
```

**Test**

```bash
# Fast unit tests — no browser or database needed:
dotnet test Surfshack.Screenshots.Testing.slnx --filter "FullyQualifiedName~.Unit."

# Full suite, including the end-to-end Playwright tests:
dotnet test Surfshack.Screenshots.Testing.slnx
```

The end-to-end DB-backed fixture reads its connection string from the
`TEST_DATABASE_CONNECTION_STRING` environment variable. CI runs the full suite on every merge
request; running the unit tests locally is usually enough while iterating.

## Project conventions

- **Public API is documented or the build fails.** Missing-doc (`CS1591`) and malformed-doc
  warnings are promoted to build *errors*, so every public type and member needs an XML doc
  comment. If you add or change public surface, document it.
- **Conventional commits** — `feat:`, `fix:`, `docs:`, `refactor:`, `chore:`, etc. Append `!`
  (e.g. `refactor!:`) for breaking changes.
- **C# style** — file-scoped namespaces, primary constructors, nullable reference types enabled,
  latest language version. Match the surrounding code.
- **LF line endings.**
- **Keep the surface small.** The library's value is that it hides complexity behind a few
  overridable members; prefer extending an existing seam over adding new public API.

## Opening a merge request

Before you open an MR, please check:

- [ ] `dotnet build` is clean — **0 warnings, 0 errors** (remember the doc gate).
- [ ] Tests pass — at least the unit tests locally; CI runs the full Playwright suite.
- [ ] New or changed public API has XML docs.
- [ ] User-facing changes are reflected in the [README](README.md).
- [ ] Breaking changes are called out in the MR description.
- [ ] Commits follow conventional-commits.

You don't need to bump the package version or edit release notes — maintainers handle versioning
(SemVer; on the `0.x` line a breaking change bumps the minor) and the release. Merging to `main`
publishes the package to NuGet via CI.

## License

By contributing, you agree that your contributions are licensed under the project's
[MIT License](LICENSE). Note that the package bundles the JetBrains Mono font under the SIL Open
Font License 1.1 — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Code of conduct

Be respectful and constructive. Assume good faith, keep discussion focused on the work, and help
make this a welcoming project to contribute to.
