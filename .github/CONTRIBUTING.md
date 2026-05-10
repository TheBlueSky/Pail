# Contributing to Pail

Thanks for contributing to Pail.

Pail is a Windows desktop app for browsing Amazon S3 buckets and objects. This guide keeps the contribution process simple and predictable.

## Before You Start

- Use Windows for development.
- Install Visual Studio 2022 or 2026.
- Install the .NET 10 SDK. The repo currently uses SDK version `10.0.203`.
- Be prepared to test UI changes locally on Windows.

## Discuss Changes First

- Open an issue before starting any code change.
- For non-trivial work, link that issue in your pull request.
- Docs-only and typo-only fixes can be opened without a prior issue.

This helps keep work aligned and avoids duplicate effort.

## Local Setup

1. Clone the repository.
2. Open the solution in Visual Studio 2022 or 2026.
3. Restore dependencies.
4. Build the solution.

Useful commands:

```bash
dotnet build
dotnet test
```

Note: the repo treats warnings as errors, so clean builds are expected.

## Making Changes

- Keep pull requests focused and limited in scope.
- Avoid mixing unrelated changes in the same branch or pull request.
- Keep Git history clean and meaningful. Use `git rebase` to tidy up commits before opening a pull request.
- Follow the existing project structure and coding style.
- Use clear, imperative commit messages such as `Add bucket filter validation`.
- For non-trivial work, split the change into multiple logical commits when that makes the review easier.
- Update documentation when behaviour, setup, or user-facing workflows change.

## Testing Expectations

Before opening a pull request:

- Make sure `dotnet build` passes.
- Make sure `dotnet test` passes.
- Run a manual smoke test for any Windows UI changes.
- Include screenshots when the UI changes.

## Pull Request Expectations

Each pull request should include:

- A short summary of the change.
- How the change was tested.
- Screenshots for UI changes.
- A related issue link for non-trivial work.

At least one maintainer approval is required before merging.

## AI-Assisted Contributions

AI-assisted contributions are allowed, but please disclose that AI tools were used when opening the pull request.

## Questions

If you are unsure whether a change is a good fit, open an issue first and describe the problem and proposed approach.
