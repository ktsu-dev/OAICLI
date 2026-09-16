// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI.Test;

/// <summary>
/// Covers the pure helpers on <see cref="CodeReviewCommand"/>: trailing newline normalization and
/// the project/solution discovery walk. Neither touches the network, so both are safe to exercise
/// on every platform.
/// </summary>
[TestClass]
public sealed class CodeReviewCommandTests
{
	/// <summary>
	/// The source files <see cref="FindCSCodeBelowCollectsSourcesRecursively"/> expects back, in the
	/// order it sorts them into.
	/// </summary>
	private static readonly string[] ExpectedSourceFileNames = ["Deep.cs", "Top.cs"];

	/// <summary>
	/// A scratch directory unique to the running test, removed during cleanup.
	/// </summary>
	private string workingDirectory = string.Empty;

	/// <summary>
	/// Creates the scratch directory the discovery tests build their fixtures inside.
	/// </summary>
	[TestInitialize]
	public void TestInitialize()
	{
		workingDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		_ = Directory.CreateDirectory(workingDirectory);
	}

	/// <summary>
	/// Removes the scratch directory so the tests stay self contained.
	/// </summary>
	[TestCleanup]
	public void TestCleanup()
	{
		if (Directory.Exists(workingDirectory))
		{
			Directory.Delete(workingDirectory, recursive: true);
		}
	}

	/// <summary>
	/// The modified content should end with exactly one newline in the original's style. The three
	/// styles are checked together because the method's whole purpose is to preserve whichever one
	/// the original file used.
	/// </summary>
	/// <param name="original">Content standing in for the file on disk.</param>
	/// <param name="modified">Content standing in for the model's reply.</param>
	/// <param name="expected">The reply after normalization.</param>
	[TestMethod]
	[DataRow("first\nsecond\n", "alpha\nbeta", "alpha\nbeta\n", DisplayName = "Unix original keeps LF")]
	[DataRow("first\r\nsecond\r\n", "alpha\nbeta", "alpha\r\nbeta\r\n", DisplayName = "Windows original keeps CRLF")]
	[DataRow("first\rsecond\r", "alpha\nbeta", "alpha\rbeta\r", DisplayName = "Mac original keeps CR")]
	public void EnsureTrainingNewLinePreservesTheOriginalLineEndingStyle(string original, string modified, string expected)
	{
		string result = CodeReviewCommand.EnsureTrainingNewLine(original, modified);

		Assert.AreEqual(expected, result);
	}

	/// <summary>
	/// The reply's own line endings must not survive. A CRLF reply to a LF file has to come back as
	/// LF, otherwise the rewritten file would be mixed.
	/// </summary>
	[TestMethod]
	public void EnsureTrainingNewLineRewritesTheModifiedContentsLineEndings()
	{
		string result = CodeReviewCommand.EnsureTrainingNewLine("first\nsecond\n", "alpha\r\nbeta\r\n");

		Assert.AreEqual("alpha\nbeta\n", result);
	}

	/// <summary>
	/// Leading and trailing whitespace on the reply is stripped before the single newline is added,
	/// so a model that pads its answer with blank lines cannot introduce them into the file.
	/// </summary>
	[TestMethod]
	public void EnsureTrainingNewLineTrimsSurroundingWhitespace()
	{
		string result = CodeReviewCommand.EnsureTrainingNewLine("first\nsecond\n", "  \n\nalpha\n\n  ");

		Assert.AreEqual("alpha\n", result);
	}

	/// <summary>
	/// A reply that already ends in a newline must not gain a second one.
	/// </summary>
	[TestMethod]
	public void EnsureTrainingNewLineDoesNotDoubleAnExistingTrailingNewline()
	{
		string result = CodeReviewCommand.EnsureTrainingNewLine("first\nsecond\n", "alpha\n");

		Assert.AreEqual("alpha\n", result);
	}

	/// <summary>
	/// Running the result back through the method must not change it again, so repeated passes over
	/// the same file converge.
	/// </summary>
	[TestMethod]
	public void EnsureTrainingNewLineIsIdempotent()
	{
		const string original = "first\r\nsecond\r\n";

		string once = CodeReviewCommand.EnsureTrainingNewLine(original, "alpha\nbeta");
		string twice = CodeReviewCommand.EnsureTrainingNewLine(original, once);

		Assert.AreEqual(once, twice);
	}

	/// <summary>
	/// An original with more than one style has no single style to preserve, and the result settles
	/// on LF rather than propagating the mixture.
	/// </summary>
	[TestMethod]
	public void EnsureTrainingNewLineSettlesAMixedOriginalOnUnixEndings()
	{
		string result = CodeReviewCommand.EnsureTrainingNewLine("first\r\nsecond\nthird\r\n", "alpha\r\nbeta");

		Assert.AreEqual("alpha\nbeta\n", result);
	}

	/// <summary>
	/// Discovery starts at the given directory and walks upward, so a solution several levels above
	/// the starting point is still found.
	/// </summary>
	[TestMethod]
	public void FindSolutionAboveWalksUpFromADirectory()
	{
		string solutionPath = Path.Combine(workingDirectory, "Sample.sln");
		File.WriteAllText(solutionPath, string.Empty);
		string nested = Path.Combine(workingDirectory, "one", "two");
		_ = Directory.CreateDirectory(nested);

		string result = CodeReviewCommand.FindSolutionAbove(nested);

		Assert.AreEqual(Path.GetFullPath(solutionPath), result);
	}

	/// <summary>
	/// When the starting point is an existing file the walk begins at its directory rather than
	/// treating the file itself as one.
	/// </summary>
	[TestMethod]
	public void FindCSProjAboveStartsFromTheDirectoryOfAFile()
	{
		string projectPath = Path.Combine(workingDirectory, "Sample.csproj");
		File.WriteAllText(projectPath, string.Empty);
		string sourcePath = Path.Combine(workingDirectory, "Program.cs");
		File.WriteAllText(sourcePath, string.Empty);

		string result = CodeReviewCommand.FindCSProjAbove(sourcePath);

		Assert.AreEqual(Path.GetFullPath(projectPath), result);
	}

	/// <summary>
	/// A pattern that matches nothing all the way to the filesystem root yields an empty string
	/// rather than throwing, which is what the callers branch on.
	/// </summary>
	[TestMethod]
	public void FindFileAboveReturnsEmptyWhenNothingMatches()
	{
		string result = CodeReviewCommand.FindFileAbove(workingDirectory, "*.ktsu-oaicli-no-such-extension");

		Assert.AreEqual(string.Empty, result);
	}

	/// <summary>
	/// The downward search is recursive, so sources nested under the starting directory are all
	/// collected while files of other types are left out.
	/// </summary>
	[TestMethod]
	public void FindCSCodeBelowCollectsSourcesRecursively()
	{
		string nested = Path.Combine(workingDirectory, "nested");
		_ = Directory.CreateDirectory(nested);
		File.WriteAllText(Path.Combine(workingDirectory, "Top.cs"), string.Empty);
		File.WriteAllText(Path.Combine(nested, "Deep.cs"), string.Empty);
		File.WriteAllText(Path.Combine(nested, "NotCode.txt"), string.Empty);

		string[] results = CodeReviewCommand.FindCSCodeBelow(workingDirectory);

		string[] names = [.. results.Select(path => Path.GetFileName(path.AsSpan()).ToString()).Order(StringComparer.Ordinal)];
		CollectionAssert.AreEqual(ExpectedSourceFileNames, names);
	}

	/// <summary>
	/// The repository layout this tool is aimed at puts the solution at the root and the project one
	/// directory below it, named after the solution. Discovery has to bridge that gap, because the
	/// upward walk from the root never sees the project file.
	/// </summary>
	[TestMethod]
	public void FindProjectAndSolutionFilePathsFallsBackToTheDirectoryNamedAfterTheSolution()
	{
		string solutionPath = Path.Combine(workingDirectory, "Sample.sln");
		File.WriteAllText(solutionPath, string.Empty);
		string projectDirectory = Path.Combine(workingDirectory, "Sample");
		_ = Directory.CreateDirectory(projectDirectory);
		string projectPath = Path.Combine(projectDirectory, "Sample.csproj");
		File.WriteAllText(projectPath, string.Empty);

		bool found = CodeReviewCommand.FindProjectAndSolutionFilePaths(workingDirectory, out string solutionFilePath, out string projectFilePath);

		Assert.IsTrue(found);
		Assert.AreEqual(Path.GetFullPath(solutionPath), solutionFilePath);
		Assert.AreEqual(Path.GetFullPath(projectPath), projectFilePath);
	}

	/// <summary>
	/// Both files sitting in the starting directory is the simple case, and neither is reported via
	/// the fallback path.
	/// </summary>
	[TestMethod]
	public void FindProjectAndSolutionFilePathsFindsBothInTheStartingDirectory()
	{
		string solutionPath = Path.Combine(workingDirectory, "Sample.sln");
		File.WriteAllText(solutionPath, string.Empty);
		string projectPath = Path.Combine(workingDirectory, "Sample.csproj");
		File.WriteAllText(projectPath, string.Empty);

		bool found = CodeReviewCommand.FindProjectAndSolutionFilePaths(workingDirectory, out string solutionFilePath, out string projectFilePath);

		Assert.IsTrue(found);
		Assert.AreEqual(Path.GetFullPath(solutionPath), solutionFilePath);
		Assert.AreEqual(Path.GetFullPath(projectPath), projectFilePath);
	}

	/// <summary>
	/// Without a solution the commands cannot decide what to send, so discovery reports failure and
	/// hands back an empty solution path.
	/// </summary>
	[TestMethod]
	public void FindProjectAndSolutionFilePathsReportsFailureWithoutASolution()
	{
		string projectPath = Path.Combine(workingDirectory, "Sample.csproj");
		File.WriteAllText(projectPath, string.Empty);

		bool found = CodeReviewCommand.FindProjectAndSolutionFilePaths(workingDirectory, out string solutionFilePath, out string projectFilePath);

		Assert.IsFalse(found);
		Assert.AreEqual(string.Empty, solutionFilePath);
		Assert.AreEqual(Path.GetFullPath(projectPath), projectFilePath);
	}

	/// <summary>
	/// The settings the commands bind to start out empty, so an invocation with no arguments does
	/// not look like one that named a file or asked to skip the confirmation.
	/// </summary>
	[TestMethod]
	public void SettingsStartEmpty()
	{
		CodeReviewCommand.Settings settings = new();

		Assert.AreEqual(string.Empty, settings.FilePath);
		Assert.AreEqual(0, settings.ContextFilePaths.Length);
		Assert.IsFalse(settings.Force);
	}
}
