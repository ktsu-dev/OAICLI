// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI.Test;

using System.Net;

/// <summary>
/// Covers what <c>oai test</c> writes into the user's solution and when: nothing until the request
/// has succeeded, and then a test project that builds.
/// </summary>
[TestClass]
public sealed class TestCommandScaffoldTests
{
	/// <summary>
	/// A scratch solution directory unique to the running test, removed during cleanup.
	/// </summary>
	private string solutionDirectory = string.Empty;

	/// <summary>
	/// Creates the scratch directory each test builds its solution inside.
	/// </summary>
	[TestInitialize]
	public void TestInitialize()
	{
		solutionDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		_ = Directory.CreateDirectory(solutionDirectory);
	}

	/// <summary>
	/// Removes the scratch directory so the tests stay self contained.
	/// </summary>
	[TestCleanup]
	public void TestCleanup()
	{
		if (Directory.Exists(solutionDirectory))
		{
			Directory.Delete(solutionDirectory, recursive: true);
		}
	}

	/// <summary>
	/// A rejected request has nothing to apply, so the command's changes must not be written.
	/// </summary>
	[TestMethod]
	public void RunDoesNotApplyTheResultWhenTheRequestFails()
	{
		bool applied = false;

		int exitCode = CodeReviewCommand.Run(
			() => throw new HttpRequestException("Unauthorized", inner: null, HttpStatusCode.Unauthorized),
			() => applied = true);

		Assert.AreEqual(CodeReviewCommand.RequestFailedExitCode, exitCode);
		Assert.IsFalse(applied, "The result was applied although the request failed");
	}

	/// <summary>
	/// A successful request is what the changes wait for.
	/// </summary>
	[TestMethod]
	public void RunAppliesTheResultAfterASuccessfulRequest()
	{
		bool applied = false;

		int exitCode = CodeReviewCommand.Run(() => "{}", () => applied = true);

		Assert.AreEqual(0, exitCode);
		Assert.IsTrue(applied, "The result was not applied after a successful request");
	}

	/// <summary>
	/// The scaffolded project names a versioned MSTest.Sdk, the framework of the project under test,
	/// a reference to it, and the MSTest namespace the scaffolded <c>[TestClass]</c> needs, so the
	/// solution still builds once it is added.
	/// </summary>
	[TestMethod]
	public void ScaffoldTestProjectWritesAProjectThatCanBuild()
	{
		WriteSolution("<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n");

		TestCommand.ScaffoldTestProject(solutionDirectory);

		string content = ReadTestProject();
		StringAssert.Contains(content, $"<Project Sdk=\"MSTest.Sdk/{TestCommand.MSTestSdkVersion}\">");
		StringAssert.Contains(content, "<TargetFramework>net8.0</TargetFramework>");
		StringAssert.Contains(content, "<ProjectReference Include=\"..\\Sample\\Sample.csproj\" />");
		StringAssert.Contains(content, "<Using Include=\"Microsoft.VisualStudio.TestTools.UnitTesting\" />");
	}

	/// <summary>
	/// A <c>global.json</c> that already pins MSTest.Sdk decides the version, so the project names the
	/// SDK without one rather than asking for a second version beside the pin.
	/// </summary>
	[TestMethod]
	public void ScaffoldTestProjectDefersToAPinnedMSTestSdk()
	{
		WriteSolution("<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n");
		File.WriteAllText(Path.Combine(solutionDirectory, "global.json"), """{ "msbuild-sdks": { "MSTest.Sdk": "4.3.3" } }""");

		TestCommand.ScaffoldTestProject(solutionDirectory);

		StringAssert.Contains(ReadTestProject(), "<Project Sdk=\"MSTest.Sdk\">");
	}

	/// <summary>
	/// The test project targets the first framework the project under test declares, unless that is a
	/// <c>netstandard</c> one or there is none, in which case it targets the running framework.
	/// </summary>
	/// <param name="projectContent">The project under test's file content.</param>
	/// <param name="expected">The framework expected, or an empty string for the running one.</param>
	[TestMethod]
	[DataRow("<TargetFramework>net8.0</TargetFramework>", "net8.0", DisplayName = "Single framework")]
	[DataRow("<TargetFrameworks>net9.0;net8.0</TargetFrameworks>", "net9.0", DisplayName = "First of several")]
	[DataRow("<TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>", "net8.0", DisplayName = "Skips netstandard")]
	[DataRow("<TargetFramework>netstandard2.1</TargetFramework>", "", DisplayName = "Only netstandard")]
	[DataRow("<TargetFrameworks></TargetFrameworks>", "", DisplayName = "Inherited from an SDK")]
	public void TestTargetFrameworkFollowsTheProjectUnderTest(string projectContent, string expected)
	{
		string running = $"net{Environment.Version.Major}.0";

		Assert.AreEqual(string.IsNullOrEmpty(expected) ? running : expected, TestCommand.TestTargetFramework(projectContent));
	}

	/// <summary>
	/// Writes <c>Sample.sln</c> and the <c>Sample</c> project it is named after.
	/// </summary>
	/// <param name="projectContent">The project file's content.</param>
	private void WriteSolution(string projectContent)
	{
		File.WriteAllText(Path.Combine(solutionDirectory, "Sample.sln"), "Microsoft Visual Studio Solution File, Format Version 12.00\nGlobal\nEndGlobal\n");
		string projectDirectory = Path.Combine(solutionDirectory, "Sample");
		_ = Directory.CreateDirectory(projectDirectory);
		File.WriteAllText(Path.Combine(projectDirectory, "Sample.csproj"), projectContent);
	}

	/// <summary>
	/// Reads back the test project the scaffold wrote.
	/// </summary>
	/// <returns>The test project file's content.</returns>
	private string ReadTestProject() =>
		File.ReadAllText(Path.Combine(solutionDirectory, "Sample.Test", "Sample.Test.csproj"));
}
