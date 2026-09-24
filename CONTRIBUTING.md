# Contributing

1. Open an issue first for anything beyond a small fix, so the API shape can be discussed.
2. Fork, branch from `main` (`feat/...`, `fix/...`).
3. `dotnet build -c Release` must pass with zero warnings (warnings are errors).
4. Add or update tests in `tests/aspire-local-ai.Tests`. Unit tests must not require Docker; mark Docker-dependent tests with `[Trait("Category", "Integration")]`.
5. Public API needs XML docs.
6. Use conventional commit messages (`feat:`, `fix:`, `docs:`, `test:`, `chore:`).
7. Open a PR against `main`; CI runs build + tests.

By contributing you agree your code is released under the MIT license.
