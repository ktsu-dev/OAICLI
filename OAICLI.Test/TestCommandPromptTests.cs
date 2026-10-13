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

	[GeneratedRegex(@"\bvar\s+\w+\s*=")]
	private static partial Regex VarDeclaration();

	[GeneratedRegex(@"(?m)^namespace\s+[\w.]+;")]
	private static partial Regex FileScopedNamespace();

	[GeneratedRegex(@"\[\w+\]\s*\r?\n\s*///")]
	private static partial Regex DocCommentAfterAttribute();
}
