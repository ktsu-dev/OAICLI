# OAICLI

A .NET CLI that uses the OpenAI Chat Completions API (`gpt-4o`) to generate unit tests and XML documentation comments for C# code in the current solution.

[![License](https://img.shields.io/github/license/ktsu-dev/OAICLI.svg?label=License&logo=nuget)](LICENSE.md)
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/ktsu-dev/OAICLI?label=Commits&logo=github)](https://github.com/ktsu-dev/OAICLI/commits/main)
[![GitHub contributors](https://img.shields.io/github/contributors/ktsu-dev/OAICLI?label=Contributors&logo=github)](https://github.com/ktsu-dev/OAICLI/graphs/contributors)
[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/ktsu-dev/OAICLI/dotnet.yml?label=Build&logo=github)](https://github.com/ktsu-dev/OAICLI/actions)

## What it does

OAICLI scans the current `.sln` and `.csproj` files, gathers C# source files, and posts them to OpenAI along with a structured prompt and response schema. The model returns JSON containing a summary, file edits, and a commit message; the tool then displays the diff and (optionally) applies the changes.

| Command | Status | Purpose |
|---|---|---|
| `test` *(default)* | available | Generate MSTest unit tests for the current solution. |
| `document` | available | Add or improve XML doc comments on C# files. |
| `code-review` | stubbed | Source exists; the file-diff and atomic-write logic is currently commented out. |

## Tech

- Built on `ktsu.Sdk.App` (multi-targeted from netstandard2.0 through net10.0).
- CLI uses Spectre.Console.Cli with `CommandLineParser` for option parsing, plus Spectre's `Json` and `ImageSharp` extensions for rich console output.
- Calls the OpenAI HTTP API directly (`POST https://api.openai.com/v1/chat/completions`) — does not depend on any OpenAI SDK.
- The expected response schema is generated via `NJsonSchema`.
- Hard-codes the `gpt-4o` model.

## Auth

On first run the tool prompts for an OpenAI API key and stores it locally via `ktsu.AppDataStorage` (typically `%APPDATA%\ktsu\OAICLI` on Windows). Subsequent runs reuse the stored key.

## Installation

```bash
git clone <repo>
cd OAICLI
dotnet build
```

## Usage

```bash
# Generate tests for the current solution (default)
OAICLI

# Generate XML doc comments for the current solution
OAICLI document

# Provide additional context files
OAICLI test --context path/to/helper.cs path/to/extension.cs

# Skip the diff preview prompt
OAICLI test --force
```

| Option | Long form | Effect |
|---|---|---|
| `-c` | `--context` | Extra context file paths to include with the request. |
| `-f` | `--force` | Skip the diff preview and accept the AI's edits. |

## License

MIT — see `LICENSE.md`.
