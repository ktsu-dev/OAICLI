// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI.Test;

using System.Text.RegularExpressions;

/// <summary>
/// Covers the instructions <c>oai test</c> sends to the model. A model that follows them and copies the
/// worked example has to end up with tests that build on MSTest 4 under the org <c>.editorconfig</c>.
/// </summary>
[TestClass]
public sealed partial class TestCommandPromptTests
{
	/// <summary>
	/// The worked example at the end of the instructions.
	/// </summary>
	private static string Example
	{
		get
		{
			string description = TestCommand.TestTaskDescription;
			int start = description.IndexOf("```csharp", StringComparison.Ordinal);
			int end = description.LastIndexOf("```", StringComparison.Ordinal);
			Assert.IsGreaterThanOrEqualTo(0, start, "The instructions should carry a C# example.");
			Assert.IsGreaterThan(start, end, "The C# example should be closed.");
			return description[start..end];
		}
	}

	/// <summary>
	/// MSTest 4 removed <c>Assert.ThrowsException</c>, so neither the rules nor the example may ask for it.
	/// </summary>
	[TestMethod]
	public void InstructionsShouldNotUseTheRemovedThrowsException()
	{
		Assert.DoesNotContain("Assert.ThrowsException", TestCommand.TestTaskDescription.Replace(
			"`Assert.ThrowsException<T>()` was removed in MSTest 4 and must not be used.",
			string.Empty,
			StringComparison.Ordinal));
		Assert.Contains("Assert.ThrowsExactly<", Example);
	}

	/// <summary>
	/// The org <c>.editorconfig</c> sent with the request makes <c>var</c> an error, so the prompt must
	/// not ask for it, and the example must not use it.
	/// </summary>
	[TestMethod]
	public void InstructionsShouldAskForExplicitTypes()
	{
		Assert.DoesNotContain("Use **var**", TestCommand.TestTaskDescription);
		Assert.DoesNotMatchRegex(VarDeclaration(), Example);
	}

	/// <summary>
	/// The org <c>.editorconfig</c> requires file-scoped namespaces, which the prompt itself also asks for.
	/// </summary>
	[TestMethod]
	public void ExampleShouldUseAFileScopedNamespace() =>
		Assert.MatchesRegex(FileScopedNamespace(), Example);

	/// <summary>
	/// A documentation comment placed after an attribute is CS1587, which the prompt's own "treat
	/// warnings as errors" rule turns into a build failure.
	/// </summary>
	[TestMethod]
	public void ExampleShouldPutDocumentationCommentsAboveAttributes() =>
		Assert.DoesNotMatchRegex(DocCommentAfterAttribute(), Example);

	/// <summary>
	/// The test and mocking libraries a model might be told to use. Each must only be named by the
	/// instructions if the scaffolded test project references it, or the faithful output fails with
	/// CS0246.
	/// </summary>
	private static readonly string[] ThirdPartyTestLibraries = ["Moq", "NSubstitute", "FakeItEasy", "FluentAssertions", "AutoFixture", "xUnit", "NUnit"];

	/// <summary>
	/// Every library the instructions name has to be referenced by the project <c>oai test</c>
	/// scaffolds for the generated tests.
	/// </summary>
	[TestMethod]
	public void InstructionsShouldOnlyNameLibrariesTheScaffoldReferences()
	{
		string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		_ = Directory.CreateDirectory(directory);
		try
		{
			string projectPath = Path.Combine(directory, "Sample", "Sample.csproj");
			_ = Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
			File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n");
			string scaffold = TestCommand.TestProjectContent(projectPath, Path.Combine(directory, "Sample.Test"), msTestSdkPinned: false, microsoftTestingPlatformSelected: false);

			foreach (string library in ThirdPartyTestLibraries.Where(library => TestCommand.TestTaskDescription.Contains(library, StringComparison.OrdinalIgnoreCase)))
			{
				Assert.Contains(library, scaffold, StringComparison.OrdinalIgnoreCase, $"The instructions name {library}, which the scaffolded test project does not reference.");
			}
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	/// <summary>
	/// The scaffold sets no <c>LangVersion</c>, so a <c>net8.0</c> test project compiles as C# 12. The
	/// instructions must not ask for a newer language version than that.
	/// </summary>
	[TestMethod]
	public void InstructionsShouldNotAskForANewerLanguageVersionThanTheScaffold() =>
		Assert.DoesNotMatchRegex(NewerLanguageVersion(), TestCommand.TestTaskDescription);

	[GeneratedRegex(@"C#\s*1[3-9]")]
	private static partial Regex NewerLanguageVersion();

	[GeneratedRegex(@"\bvar\s+\w+\s*=")]
	private static partial Regex VarDeclaration();

	[GeneratedRegex(@"(?m)^namespace\s+[\w.]+;")]
	private static partial Regex FileScopedNamespace();

	[GeneratedRegex(@"\[\w+\]\s*\r?\n\s*///")]
	private static partial Regex DocCommentAfterAttribute();
}
