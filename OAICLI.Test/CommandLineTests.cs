// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI.Test;

using Spectre.Console.Cli;

/// <summary>
/// Covers how the command line is parsed, using the application's own command configuration with a
/// probe command added that records whether it ran instead of sending a request.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CommandLineTests
{
	/// <summary>
	/// Resets the probe before each test.
	/// </summary>
	[TestInitialize]
	public void TestInitialize() => ProbeCommand.Ran = false;

	/// <summary>
	/// Inputs the commands used to accept and then ignore must now be refused before the command
	/// runs, with a non-zero exit code, rather than the command running as if they had not been given.
	/// </summary>
	/// <param name="argument">The ignored input.</param>
	[TestMethod]
	[DataRow("--force", DisplayName = "--force")]
	[DataRow("-f", DisplayName = "-f")]
	[DataRow("--context", "helper.cs", DisplayName = "--context")]
	[DataRow("-c", "helper.cs", DisplayName = "-c")]
	[DataRow("path/to/Program.cs", DisplayName = "file path")]
	public void IgnoredInputsAreRejectedBeforeTheCommandRuns(params string[] argument)
	{
		int exitCode = Run(["probe", .. argument]);

		Assert.AreNotEqual(0, exitCode, "An input the command cannot honour must fail the run.");
		Assert.IsFalse(ProbeCommand.Ran, "The command must not run with an input it would ignore.");
	}

	/// <summary>
	/// The probe runs when it is given nothing, which shows the rejection above comes from the input
	/// and not from the probe's wiring, and that the application's examples still validate.
	/// </summary>
	[TestMethod]
	public void ACommandWithNoInputsRuns()
	{
		_ = Run(["probe"]);

		Assert.IsTrue(ProbeCommand.Ran);
	}

	private static int Run(string[] args)
	{
		CommandApp app = new();
		app.Configure(config =>
		{
			OAI.Configure(config);
			_ = config.AddCommand<ProbeCommand>("probe");
		});

		return app.Run(args);
	}

	/// <summary>
	/// A command that notes it was reached and stops there, so no request is ever sent.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by Spectre.Console.Cli when the probe command is run.")]
	internal sealed class ProbeCommand : CodeReviewCommand
	{
		internal static bool Ran { get; set; }

		internal override Request TaskRequest
		{
			get
			{
				Ran = true;
				throw new InvalidOperationException("Probe stops before sending a request.");
			}
		}
	}
}
