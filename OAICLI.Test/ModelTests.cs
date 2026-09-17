// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI.Test;

using System.Text.Json.Nodes;

/// <summary>
/// Covers the request and reply models. Every string on them defaults to empty and every collection
/// to an empty one, which is what lets the commands build a request up field by field without
/// guarding against nulls.
/// </summary>
[TestClass]
public sealed class ModelTests
{
	/// <summary>
	/// A new request carries no name, no description, and no files.
	/// </summary>
	[TestMethod]
	public void RequestStartsEmpty()
	{
		Request request = new();

		Assert.AreEqual(string.Empty, request.Name);
		Assert.AreEqual(string.Empty, request.Description);
		Assert.AreEqual(0, request.Files.Count);
	}

	/// <summary>
	/// The file collection is mutable, because the commands populate it by walking the repository.
	/// </summary>
	[TestMethod]
	public void RequestAcceptsFiles()
	{
		Request request = new();

		request.Files.Add(new() { FilePath = "src/Program.cs", Purpose = "C# Code File", Contents = "// code" });

		Assert.AreEqual(1, request.Files.Count);
		Assert.AreEqual("src/Program.cs", request.Files[0].FilePath);
	}

	/// <summary>
	/// A new file definition has a path, a purpose, and contents, all empty.
	/// </summary>
	[TestMethod]
	public void FileDefinitionStartsEmpty()
	{
		FileDefinition file = new();

		Assert.AreEqual(string.Empty, file.FilePath);
		Assert.AreEqual(string.Empty, file.Purpose);
		Assert.AreEqual(string.Empty, file.Contents);
	}

	/// <summary>
	/// A new reply carries no summary, no files, and no commit message.
	/// </summary>
	[TestMethod]
	public void ResponseStartsEmpty()
	{
		Response response = new();

		Assert.AreEqual(string.Empty, response.Summary);
		Assert.AreEqual(0, response.Files.Count);
		Assert.AreEqual(string.Empty, response.CommitMessage);
	}

	/// <summary>
	/// The response format asks for a JSON schema and carries the schema derived from
	/// <see cref="Response"/>, which is how the reply is constrained to a shape we can parse.
	/// </summary>
	[TestMethod]
	public void ResponseFormatRequestsASchemaDerivedFromResponse()
	{
		ResponseFormat format = new();

		Assert.AreEqual("json_schema", format.Type);
		Assert.IsNotNull(format.Schema);

		JsonNode? properties = format.Schema["properties"];
		Assert.IsNotNull(properties);
		Assert.IsNotNull(properties["Summary"]);
		Assert.IsNotNull(properties["Files"]);
		Assert.IsNotNull(properties["CommitMessage"]);
	}

	/// <summary>
	/// A new message defaults to the developer role with no content.
	/// </summary>
	[TestMethod]
	public void MessageStartsEmpty()
	{
		Message message = new();

		Assert.AreEqual(MessageRole.Developer, message.Role);
		Assert.AreEqual(0, message.Content.Length);
	}

	/// <summary>
	/// A new content entry is text, and empty.
	/// </summary>
	[TestMethod]
	public void MessageContentStartsAsEmptyText()
	{
		MessageContent content = new();

		Assert.AreEqual(MessageContentType.Text, content.Type);
		Assert.AreEqual(string.Empty, content.Text);
	}

	/// <summary>
	/// A new request body names no model and carries no messages, but it always asks for a
	/// structured reply, because the caller never sets that itself.
	/// </summary>
	[TestMethod]
	public void RequestBodyStartsEmptyButAlwaysAsksForAStructuredReply()
	{
		RequestBody requestBody = new();

		Assert.AreEqual(string.Empty, requestBody.Model);
		Assert.AreEqual(0, requestBody.Messages.Length);
		Assert.IsNotNull(requestBody.ResponseFormat);
		Assert.AreEqual("json_schema", requestBody.ResponseFormat.Type);
	}

	/// <summary>
	/// The developer prompt is what tells the model it is being handed a coding task in JSON, so it
	/// should not be blank.
	/// </summary>
	[TestMethod]
	public void DeveloperPromptIsNotBlank()
	{
		Assert.IsFalse(string.IsNullOrWhiteSpace(OAI.DeveloperPrompt));
	}
}
