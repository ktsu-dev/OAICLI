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

	/// <summary>
	/// A solution with two projects: the ordinary case, and the one the old insertion could not
	/// survive. CRLF throughout, as a solution file written by Visual Studio is.
	/// </summary>
	private const string TwoProjectSolution =
		"Microsoft Visual Studio Solution File, Format Version 12.00\r\n" +
		"Project(\"{9A19103F-16F7-4668-BE54-9A1E7A4F7556}\") = \"App\", \"App\\App.csproj\", \"{11111111-1111-1111-1111-111111111111}\"\r\n" +
		"EndProject\r\n" +
		"Project(\"{9A19103F-16F7-4668-BE54-9A1E7A4F7556}\") = \"Lib\", \"Lib\\Lib.csproj\", \"{22222222-2222-2222-2222-222222222222}\"\r\n" +
		"EndProject\r\n" +
		"Global\r\n" +
		"\tGlobalSection(SolutionProperties) = preSolution\r\n" +
		"\t\tHideSolutionNode = FALSE\r\n" +
		"\tEndGlobalSection\r\n" +
		"EndGlobal\r\n";

	/// <summary>
	/// The entry to add, in the Unix-newline form the caller passes.
	/// </summary>
	private const string NewProjectEntry =
		"Project(\"{9A19103F-16F7-4668-BE54-9A1E7A4F7556}\") = \"App.Test\", \"App.Test\\App.Test.csproj\", \"{33333333-3333-3333-3333-333333333333}\"\nEndProject";

	/// <summary>
	/// The entry must land once, however many projects the solution already has. Inserting it after
	/// every existing EndProject gave each copy the same GUID, which neither Visual Studio nor
	/// <c>dotnet sln</c> will load.
	/// </summary>
	[TestMethod]
	public void InsertProjectEntryAddsTheEntryExactlyOnce()
	{
		string result = CodeReviewCommand.InsertProjectEntry(TwoProjectSolution, NewProjectEntry);

		Assert.AreEqual(1, CountOccurrences(result, "App.Test\\App.Test.csproj"));
		Assert.AreEqual(1, CountOccurrences(result, "{33333333-3333-3333-3333-333333333333}"));
	}

	/// <summary>
	/// Every project must still be terminated afterwards. The old insertion consumed each existing
	/// EndProject as the text it replaced, leaving the projects ahead of it unterminated.
	/// </summary>
	[TestMethod]
	public void InsertProjectEntryKeepsEveryExistingProjectTerminated()
	{
		string result = CodeReviewCommand.InsertProjectEntry(TwoProjectSolution, NewProjectEntry);

		Assert.AreEqual(3, CountOccurrences(result, "Project(\""), "Two existing projects plus the new one.");
		Assert.AreEqual(3, CountLines(result, "EndProject"), "One terminator per project.");
	}

	/// <summary>
	/// A solution folder's nested-project section must come through untouched. "EndProject" is a
	/// prefix of "EndProjectSection", so replacing the former rewrote the latter as well.
	/// </summary>
	[TestMethod]
	public void InsertProjectEntryLeavesNestedProjectSectionsIntact()
	{
		string withSection =
			"Microsoft Visual Studio Solution File, Format Version 12.00\r\n" +
			"Project(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \"Solution Items\", \"Solution Items\", \"{44444444-4444-4444-4444-444444444444}\"\r\n" +
			"\tProjectSection(SolutionItems) = preProject\r\n" +
			"\t\tREADME.md = README.md\r\n" +
			"\tEndProjectSection\r\n" +
			"EndProject\r\n" +
			"Global\r\n" +
			"EndGlobal\r\n";

		string result = CodeReviewCommand.InsertProjectEntry(withSection, NewProjectEntry);

		// The section markers survive even a blanket replace, so the count is what discriminates:
		// "EndProject" occurs twice in this fixture -- once inside "EndProjectSection" -- and a
		// replace of both inserts the entry twice, once of them splitting the section apart.
		Assert.AreEqual(2, CountOccurrences(result, "Project(\""), "The solution folder plus the new project.");
		Assert.AreEqual(1, CountLines(result, "ProjectSection(SolutionItems) = preProject"));
		Assert.AreEqual(1, CountLines(result, "EndProjectSection"));
		Assert.AreEqual(2, CountLines(result, "EndProject"), "One terminator for the folder, one for the new project.");
		Assert.Contains("\t\tREADME.md = README.md", result);
	}

	/// <summary>
	/// The entry belongs in the project list, which ends where the global section starts.
	/// </summary>
	[TestMethod]
	public void InsertProjectEntryPutsTheEntryBeforeTheGlobalSection()
	{
		string result = CodeReviewCommand.InsertProjectEntry(TwoProjectSolution, NewProjectEntry);

		int entryIndex = result.IndexOf("App.Test", StringComparison.Ordinal);
		int globalIndex = result.IndexOf("\r\nGlobal\r\n", StringComparison.Ordinal);

		Assert.IsGreaterThan(-1, entryIndex);
		Assert.IsGreaterThan(-1, globalIndex);
		Assert.IsLessThan(globalIndex, entryIndex, "The new project must be declared before the global section.");
	}

	/// <summary>
	/// A solution with no global section has nothing to sit in front of, so the entry goes last. Both
	/// trailing-newline shapes are checked, since the entry has to start on a line of its own either
	/// way.
	/// </summary>
	[TestMethod]
	public void InsertProjectEntryAppendsWhenThereIsNoGlobalSection()
	{
		const string withoutGlobal =
			"Microsoft Visual Studio Solution File, Format Version 12.00\r\n" +
			"Project(\"{9A19103F-16F7-4668-BE54-9A1E7A4F7556}\") = \"App\", \"App\\App.csproj\", \"{11111111-1111-1111-1111-111111111111}\"\r\n" +
			"EndProject\r\n";

		string terminated = CodeReviewCommand.InsertProjectEntry(withoutGlobal, NewProjectEntry);
		string unterminated = CodeReviewCommand.InsertProjectEntry(withoutGlobal.TrimEnd('\r', '\n'), NewProjectEntry);

		Assert.StartsWith(withoutGlobal, terminated, "The existing content should be left as it was, with the entry added after it.");
		Assert.AreEqual(2, CountLines(terminated, "EndProject"), "The existing project plus the new one.");
		Assert.AreEqual(2, CountLines(unterminated, "EndProject"), "A missing final newline must not run the two entries together.");
	}

	/// <summary>
	/// The whole of <see cref="TestCommand"/>'s setup against a real solution on disk. The layout it
	/// expects is a solution at the root with the project one directory below, named after it. This is
	/// the path the corruption actually reached, so it is worth covering end to end rather than only
	/// through the helper.
	/// </summary>
	[TestMethod]
	public void TestCommandSetupAddsTheTestProjectToTheSolutionOnce()
	{
		// Arrange
		string solutionPath = Path.Combine(workingDirectory, "Sample.sln");
		File.WriteAllText(solutionPath, TwoProjectSolution);
		string projectDirectory = Path.Combine(workingDirectory, "Sample");
		_ = Directory.CreateDirectory(projectDirectory);
		File.WriteAllText(Path.Combine(projectDirectory, "Sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\r\n");

		// Act -- setup discovers from the current directory, so it has to be moved and put back
		string originalDirectory = Directory.GetCurrentDirectory();
		try
		{
			Directory.SetCurrentDirectory(workingDirectory);
			new TestCommand().Setup(new CodeReviewCommand.Settings());
		}
		finally
		{
			Directory.SetCurrentDirectory(originalDirectory);
		}

		// Assert -- one new entry, and every project still terminated
		string updated = File.ReadAllText(solutionPath);
		Assert.AreEqual(1, CountOccurrences(updated, "Sample.Test\\Sample.Test.csproj"), "The entry must be added exactly once.");
		Assert.AreEqual(3, CountOccurrences(updated, "Project(\""), "The two existing projects plus the new one.");
		Assert.AreEqual(3, CountLines(updated, "EndProject"), "One terminator per project.");
		Assert.IsTrue(File.Exists(Path.Combine(workingDirectory, "Sample.Test", "Sample.Test.csproj")), "The test project itself should have been written.");
	}

	/// <summary>
	/// The edit must not leave the file carrying two newline conventions at once.
	/// </summary>
	[TestMethod]
	public void InsertProjectEntryKeepsTheSolutionsLineEndings()
	{
		string crlf = CodeReviewCommand.InsertProjectEntry(TwoProjectSolution, NewProjectEntry);
		string lf = CodeReviewCommand.InsertProjectEntry(TwoProjectSolution.Replace("\r\n", "\n", StringComparison.Ordinal), NewProjectEntry);

		Assert.AreEqual(CountOccurrences(crlf, "\n"), CountOccurrences(crlf, "\r\n"), "A CRLF solution should gain no lone newline.");
		Assert.AreEqual(0, CountOccurrences(lf, "\r"), "An LF solution should gain no carriage return.");
	}

	/// <summary>
	/// Counts the lines that are exactly the given text, ignoring indentation. Substring counting
	/// will not do here, because "EndProject" is a prefix of "EndProjectSection".
	/// </summary>
	/// <param name="content">The content to search.</param>
	/// <param name="line">The line to count.</param>
	/// <returns>The number of matching lines.</returns>
	private static int CountLines(string content, string line) =>
		content.Split('\n').Count(candidate => candidate.Trim() == line);

	/// <summary>
	/// Counts non-overlapping occurrences of a substring.
	/// </summary>
	/// <param name="content">The content to search.</param>
	/// <param name="value">The substring to count.</param>
	/// <returns>The number of occurrences.</returns>
	private static int CountOccurrences(string content, string value)
	{
		int count = 0;
		int index = content.IndexOf(value, StringComparison.Ordinal);
		while (index >= 0)
		{
			count++;
			index = content.IndexOf(value, index + value.Length, StringComparison.Ordinal);
		}

		return count;
	}
}
