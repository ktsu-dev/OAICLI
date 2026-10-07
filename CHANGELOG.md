## v1.1.7

No significant changes detected since v1.1.7.

## v1.1.7 (patch)

Changes since v1.1.6:

- fix: mask the API key as it is typed at the first-run prompt [patch] ([@Claude](https://github.com/Claude))

## v1.1.6 (patch)

Changes since v1.1.5:

- fix: find a project under src/ instead of crashing on a missing directory [patch] ([@Claude](https://github.com/Claude))

## v1.1.5 (patch)

Changes since v1.1.4:

- fix: leave build output and generated sources out of document and test requests [patch] ([@Claude](https://github.com/Claude))

## v1.1.4 (patch)

Changes since v1.1.3:

- fix: recognise a missing libsecret behind a TypeInitializationException [patch] ([@Claude](https://github.com/Claude))

## v1.1.3 (patch)

Changes since v1.1.2:

- Move CI onto the shared ci-shared.yml pipeline ([@Claude](https://github.com/Claude))

## v1.1.2 (patch)

Changes since v1.1.1:

- fix: report a non-JSON error body with its status instead of crashing [patch] ([@Claude](https://github.com/Claude))

## v1.1.1 (patch)

Changes since v1.1.0:

- fix: register the generated test project so solution builds include it [patch] ([@Claude](https://github.com/Claude))
- fix: nest the response schema under response_format.json_schema [patch] ([@Claude](https://github.com/Claude))

## v1.1.0 (minor)

Changes since v1.0.0:

- refactor: fold the timeout into Describe's final arm ([@Claude](https://github.com/Claude))
- refactor: report rejections as HttpRequestException, not a bespoke type ([@Claude](https://github.com/Claude))
- test: cover RequestFailedException's constructors and the wrapped rejection ([@Claude](https://github.com/Claude))
- fix: exit non-zero when the OpenAI API rejects a request [patch] ([@Claude](https://github.com/Claude))
- test: cover the no-global-section branch and the setup path end to end [patch] ([@Claude](https://github.com/Claude))
- fix: insert the test project into a solution exactly once [patch] ([@Claude](https://github.com/Claude))
- test: cover the prompt loop and the legacy app data store ([@Claude](https://github.com/Claude))
- feat: keep the OpenAI API key in the OS secret store [minor] ([@Claude](https://github.com/Claude))

## v1.0.1 (patch)

Changes since v1.0.0:

- refactor: fold the timeout into Describe's final arm ([@Claude](https://github.com/Claude))
- refactor: report rejections as HttpRequestException, not a bespoke type ([@Claude](https://github.com/Claude))
- test: cover RequestFailedException's constructors and the wrapped rejection ([@Claude](https://github.com/Claude))
- fix: exit non-zero when the OpenAI API rejects a request [patch] ([@Claude](https://github.com/Claude))
- test: cover the no-global-section branch and the setup path end to end [patch] ([@Claude](https://github.com/Claude))
- fix: insert the test project into a solution exactly once [patch] ([@Claude](https://github.com/Claude))

## v1.0.0 (major)

- fix: bump ktsu.Extensions and ktsu.AppDataStorage off the leaked SourceLink ([@matt-edmondson](https://github.com/matt-edmondson))
- test: populate OAICLI.Test so discovery finds tests to run ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: carry the response schema as JSON so a request can be serialized ([@matt-edmondson](https://github.com/matt-edmondson))
- Gate Dependabot auto-merge on CI actually being green ([@Claude](https://github.com/Claude))
- ci: adopt the consolidated .NET workflow [patch] ([@Claude](https://github.com/Claude))
- ci: make the SonarQube quality gate opt in [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- ci: adopt the unified dotnet workflow [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- chore: store icon.png in LFS as .gitattributes declares ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: scope build badge to the default branch ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: correct README, DESCRIPTION and TAGS metadata ([@matt-edmondson](https://github.com/matt-edmondson))
- Migrate to ktsu.Sdk 2.x layout and fix analyzer errors: modern SDK csproj format, CPM cleanup, Spectre.Console 0.57 API update, new file headers, LF endings [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- Sync .github\workflows\dotnet.yml ([@KtsuTools](https://github.com/KtsuTools))
- Sync .runsettings ([@KtsuTools](https://github.com/KtsuTools))
- Sync .editorconfig ([@KtsuTools](https://github.com/KtsuTools))
- Sync .gitattributes ([@KtsuTools](https://github.com/KtsuTools))
- Sync global.json ([@KtsuTools](https://github.com/KtsuTools))
- Sync .github\workflows\dotnet.yml ([@KtsuTools](https://github.com/KtsuTools))
- Update packages ([@matt-edmondson](https://github.com/matt-edmondson))
- Add TAGS.md with NuGet package tags ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: replace placeholder README with usage and command reference ([@matt-edmondson](https://github.com/matt-edmondson))
- Sync .github\workflows\dotnet.yml ([@KtsuTools](https://github.com/KtsuTools))
- Remove legacy build scripts ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove .github\workflows\project.yml ([@matt-edmondson](https://github.com/matt-edmondson))
- Update .editorconfig, .gitignore, .gitattributes, .mailmap, .runsettings, and scripts/PSBuild.psm1 for configuration and script improvements ([@matt-edmondson](https://github.com/matt-edmondson))
- Update package references in OAICLI.csproj to latest versions ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove Directory.Build.props and Directory.Build.targets files; add copyright headers to OAICLI source files. ([@matt-edmondson](https://github.com/matt-edmondson))
- Update project SDK references in OAICLI and OAICLI.Test to ktsu.Sdk.App and ktsu.Sdk.Test respectively ([@matt-edmondson](https://github.com/matt-edmondson))
- Add core functionality for API interaction and message handling ([@matt-edmondson](https://github.com/matt-edmondson))
- Update packages ([@matt-edmondson](https://github.com/matt-edmondson))
- Add LICENSE template ([@matt-edmondson](https://github.com/matt-edmondson))
- Rename OAICLI class to OAI and update references ([@matt-edmondson](https://github.com/matt-edmondson))
- Update README.md ([@matt-edmondson](https://github.com/matt-edmondson))
- Initial commit ([@matt-edmondson](https://github.com/matt-edmondson))

