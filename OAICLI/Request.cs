// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI;

using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Json;

internal class Request
{
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public Collection<FileDefinition> Files { get; set; } = [];

	internal string Send()
	{
		Auth.EnsureHasApiKey();

		using HttpClient client = Auth.GetClient();

		RequestBody requestBody = new()
		{
			Model = "gpt-4o",
			Messages =
			[
				new()
				{
					Role = MessageRole.Developer,
					Content =
					[
						new()
						{
							Type = MessageContentType.Text,
							Text = "You are a helpful, expert coding assistant. You will be performing coding tasks which you will receive in a json format.",
						},
					],
				},
				new()
				{
					Role = MessageRole.User,
					Content =
					[
						new()
						{
							Type = MessageContentType.Text,
							Text = JsonSerializer.Serialize(this, Json.SerializerOptions),
						},
					],
				},
			],
		};

		string requestJson = JsonSerializer.Serialize(requestBody, Json.SerializerOptions);
		AnsiConsole.Write(new Panel(new JsonText(requestJson)).BorderColor(Color.Green).Header("Request"));

		using StringContent content = new(requestJson, Encoding.UTF8, "application/json");
		Uri requestURI = new("https://api.openai.com/v1/chat/completions");
		using HttpResponseMessage response = client.PostAsync(requestURI, content).Result;

		string responseJson = response.Content.ReadAsStringAsync().Result;
		return EnsureSuccessful(response.StatusCode, response.ReasonPhrase, responseJson);
	}

	/// <summary>
	/// Reports the body of a response the API accepted, or throws for one it rejected.
	/// </summary>
	/// <remarks>
	/// An error body is JSON too, so printing it without looking at the status code makes a rejected
	/// request indistinguishable from an accepted one. Callers branch on the exception to decide the
	/// process exit code, which is what a script gating on this tool reads.
	/// </remarks>
	/// <param name="statusCode">The status code the API answered with.</param>
	/// <param name="reasonPhrase">The reason phrase accompanying <paramref name="statusCode"/>, if any.</param>
	/// <param name="responseJson">The body of the response, error or otherwise.</param>
	/// <returns><paramref name="responseJson"/>, when the request succeeded.</returns>
	/// <exception cref="HttpRequestException">
	/// The request did not succeed. Its <see cref="HttpRequestException.StatusCode"/> carries the
	/// status the API answered with, which is what distinguishes a rejected request from one that
	/// never reached the API at all.
	/// </exception>
	internal static string EnsureSuccessful(HttpStatusCode statusCode, string? reasonPhrase, string responseJson)
	{
		bool succeeded = (int)statusCode is >= 200 and <= 299;
		string status = $"{(int)statusCode} {reasonPhrase ?? statusCode.ToString()}";

		AnsiConsole.Write(new Panel(new JsonText(responseJson))
			.BorderColor(succeeded ? Color.Green : Color.Red)
			.Header(succeeded ? "Response" : $"Response ({status})"));

		return succeeded
			? responseJson
			: throw new HttpRequestException(
				$"The OpenAI API request failed with {status}: {responseJson}",
				inner: null,
				statusCode);
	}
}

/// <summary>
/// Represents the body of a request to the OpenAI API.
/// </summary>
internal class RequestBody
{
	/// <summary>
	/// Gets or sets the model to be used for the request.
	/// </summary>
	public string Model { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the messages to be sent in the request.
	/// </summary>
	public Message[] Messages { get; set; } = [];

	public ResponseFormat ResponseFormat { get; set; } = new();
}
