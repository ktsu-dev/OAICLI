// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI.Test;

using System.Net;

/// <summary>
/// Covers the two halves of turning a rejected API request into a visible failure: the response
/// check in <see cref="Request"/>, and the exit code <see cref="CodeReviewCommand"/> derives from
/// it. Neither half touches the network.
/// </summary>
[TestClass]
public sealed class RequestFailureTests
{
	/// <summary>
	/// A body standing in for the error JSON the API returns alongside a rejection.
	/// </summary>
	private const string ErrorBody = """{"error":{"message":"Incorrect API key provided.","code":"invalid_api_key"}}""";

	/// <summary>
	/// A body standing in for a completion the API accepted.
	/// </summary>
	private const string SuccessBody = """{"choices":[{"message":{"content":"done"}}]}""";

	/// <summary>
	/// An accepted response hands its body back unchanged, so the success path is untouched by the
	/// check.
	/// </summary>
	/// <param name="statusCode">A status code in the success range.</param>
	[TestMethod]
	[DataRow(HttpStatusCode.OK, DisplayName = "200 OK")]
	[DataRow(HttpStatusCode.Created, DisplayName = "201 Created")]
	[DataRow(HttpStatusCode.NoContent, DisplayName = "204 No Content")]
	public void EnsureSuccessfulReturnsTheBodyOfAnAcceptedResponse(HttpStatusCode statusCode)
	{
		string result = Request.EnsureSuccessful(statusCode, "OK", SuccessBody);

		Assert.AreEqual(SuccessBody, result);
	}

	/// <summary>
	/// The failure this guards against: an error body is JSON too, so without the status code check
	/// a rejection reads exactly like a completion. Each of these has to throw rather than return.
	/// </summary>
	/// <param name="statusCode">A status code outside the success range.</param>
	[TestMethod]
	[DataRow(HttpStatusCode.Unauthorized, DisplayName = "401 from an invalid or expired key")]
	[DataRow(HttpStatusCode.TooManyRequests, DisplayName = "429 from a rate limit")]
	[DataRow(HttpStatusCode.BadRequest, DisplayName = "400 from a malformed request")]
	[DataRow(HttpStatusCode.InternalServerError, DisplayName = "500 from the API")]
	[DataRow(HttpStatusCode.MultipleChoices, DisplayName = "300, just outside the success range")]
	public void EnsureSuccessfulThrowsForARejectedResponse(HttpStatusCode statusCode)
	{
		_ = Assert.ThrowsExactly<RequestFailedException>(
			() => Request.EnsureSuccessful(statusCode, "Unauthorized", ErrorBody));
	}

	/// <summary>
	/// The thrown exception carries the status code and the API's own error body, which is what makes
	/// the reported failure diagnosable rather than just non-zero.
	/// </summary>
	[TestMethod]
	public void EnsureSuccessfulCarriesTheStatusCodeAndBodyOnTheException()
	{
		RequestFailedException exception = Assert.ThrowsExactly<RequestFailedException>(
			() => Request.EnsureSuccessful(HttpStatusCode.Unauthorized, "Unauthorized", ErrorBody));

		Assert.AreEqual(HttpStatusCode.Unauthorized, exception.StatusCode);
		Assert.AreEqual(ErrorBody, exception.ResponseBody);
		StringAssert.Contains(exception.Message, "401", StringComparison.Ordinal);
	}

	/// <summary>
	/// A response with no reason phrase still names the status, so the message never degrades to a
	/// bare number with an empty label.
	/// </summary>
	[TestMethod]
	public void EnsureSuccessfulNamesTheStatusWhenThereIsNoReasonPhrase()
	{
		RequestFailedException exception = Assert.ThrowsExactly<RequestFailedException>(
			() => Request.EnsureSuccessful(HttpStatusCode.TooManyRequests, null, ErrorBody));

		StringAssert.Contains(exception.Message, nameof(HttpStatusCode.TooManyRequests), StringComparison.Ordinal);
	}

	/// <summary>
	/// The exception's standard constructors, which <c>CA1032</c> requires it to carry alongside the
	/// one the status check uses. A default-constructed instance reports no status and an empty body
	/// rather than a null one, so a handler can read <see cref="RequestFailedException.ResponseBody"/>
	/// without a null check whichever constructor was used.
	/// </summary>
	[TestMethod]
	public void ADefaultConstructedExceptionCarriesNoStatusAndAnEmptyBody()
	{
		RequestFailedException exception = new();

		Assert.IsNull(exception.StatusCode);
		Assert.AreEqual(string.Empty, exception.ResponseBody);
	}

	/// <summary>
	/// The message-only constructor keeps the message and still reports no status.
	/// </summary>
	[TestMethod]
	public void AMessageOnlyExceptionKeepsItsMessage()
	{
		RequestFailedException exception = new("the request failed");

		Assert.AreEqual("the request failed", exception.Message);
		Assert.IsNull(exception.StatusCode);
		Assert.AreEqual(string.Empty, exception.ResponseBody);
	}

	/// <summary>
	/// The wrapping constructor keeps both the message and the exception that caused the failure.
	/// </summary>
	[TestMethod]
	public void AWrappingExceptionKeepsItsInnerException()
	{
		HttpRequestException inner = new("Connection refused.");

		RequestFailedException exception = new("the request failed", inner);

		Assert.AreEqual("the request failed", exception.Message);
		Assert.AreSame(inner, exception.InnerException);
	}

	/// <summary>
	/// A request that came back successfully still exits zero.
	/// </summary>
	[TestMethod]
	public void SendRequestReportsSuccessWhenTheRequestSucceeds()
	{
		int exitCode = CodeReviewCommand.SendRequest(() => SuccessBody);

		Assert.AreEqual(0, exitCode);
	}

	/// <summary>
	/// The issue's acceptance criterion: a rejected request — an invalid key, say — has to exit
	/// non-zero, because the exit status is the only thing a CI job gating on this tool can read.
	/// </summary>
	[TestMethod]
	public void SendRequestReportsFailureWhenTheApiRejectsTheRequest()
	{
		int exitCode = CodeReviewCommand.SendRequest(
			() => Request.EnsureSuccessful(HttpStatusCode.Unauthorized, "Unauthorized", ErrorBody));

		Assert.AreEqual(CodeReviewCommand.RequestFailedExitCode, exitCode);
		Assert.AreNotEqual(0, exitCode);
	}

	/// <summary>
	/// A request that never reached the API — no network, DNS failure, a refused connection — is a
	/// failure on the same terms.
	/// </summary>
	[TestMethod]
	public void SendRequestReportsFailureWhenTheRequestCannotBeSent()
	{
		int exitCode = CodeReviewCommand.SendRequest(
			() => throw new HttpRequestException("No such host is known."));

		Assert.AreEqual(CodeReviewCommand.RequestFailedExitCode, exitCode);
	}

	/// <summary>
	/// The send path blocks on the task, so a transport failure arrives wrapped. Unwrapping it here
	/// keeps a network error from escaping as an unhandled exception instead of an exit code.
	/// </summary>
	[TestMethod]
	public void SendRequestReportsFailureWhenTheTransportErrorArrivesWrapped()
	{
		int exitCode = CodeReviewCommand.SendRequest(
			() => throw new AggregateException(new HttpRequestException("Connection refused.")));

		Assert.AreEqual(CodeReviewCommand.RequestFailedExitCode, exitCode);
	}

	/// <summary>
	/// A rejection that arrives wrapped is unwrapped on the same terms as a transport failure, so the
	/// exit code does not depend on where in the blocking send path the failure surfaced.
	/// </summary>
	[TestMethod]
	public void SendRequestReportsFailureWhenTheRejectionArrivesWrapped()
	{
		int exitCode = CodeReviewCommand.SendRequest(
			() => throw new AggregateException(
				new RequestFailedException(HttpStatusCode.Unauthorized, "Unauthorized", ErrorBody)));

		Assert.AreEqual(CodeReviewCommand.RequestFailedExitCode, exitCode);
	}

	/// <summary>
	/// A wrapped failure of some other kind is not the request's own, and must not be flattened into
	/// an exit code that hides it.
	/// </summary>
	[TestMethod]
	public void SendRequestDoesNotSwallowAnUnrelatedWrappedFailure()
	{
		_ = Assert.ThrowsExactly<AggregateException>(
			() => CodeReviewCommand.SendRequest(
				() => throw new AggregateException(new InvalidOperationException("no solution found"))));
	}

	/// <summary>
	/// A request that timed out is reported rather than thrown, for the same reason.
	/// </summary>
	[TestMethod]
	public void SendRequestReportsFailureWhenTheRequestTimesOut()
	{
		int exitCode = CodeReviewCommand.SendRequest(
			() => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));

		Assert.AreEqual(CodeReviewCommand.RequestFailedExitCode, exitCode);
	}

	/// <summary>
	/// Failures that are not the request's own — a bug in building it, say — must still surface as
	/// exceptions rather than being flattened into an exit code that hides them.
	/// </summary>
	[TestMethod]
	public void SendRequestDoesNotSwallowAnUnrelatedFailure()
	{
		_ = Assert.ThrowsExactly<InvalidOperationException>(
			() => CodeReviewCommand.SendRequest(
				() => throw new InvalidOperationException("Could not find project and solution files.")));
	}

	/// <summary>
	/// The guard on the argument is the one the analyzers require, and a null sender is a programming
	/// error rather than a request failure.
	/// </summary>
	[TestMethod]
	public void SendRequestRejectsANullSender()
	{
		_ = Assert.ThrowsExactly<ArgumentNullException>(() => CodeReviewCommand.SendRequest(null!));
	}
}
