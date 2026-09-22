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

