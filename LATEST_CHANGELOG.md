## v1.2.1 (patch)

Changes since v1.2.0:

- Find src/\<Name\>/\<Name\>.csproj past an empty \<Name\>/ folder, and never pick a project above the solution [patch] (#96) (matt-edmondson)
- Send source characters to the model as themselves, not as \u escapes [patch] (#98) (matt-edmondson)
- Stop shipping CommandLineParser and Spectre.Console.Testing in the tool [patch] (#99) (matt-edmondson)
- Refuse to scaffold tests for a test project, instead of adding \<Project\>.Test.Test [patch] (#100) (matt-edmondson)

