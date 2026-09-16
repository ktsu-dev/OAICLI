// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.OAICLI.Test;

using System.Text.Json;

/// <summary>
/// Covers <see cref="Json.SerializerOptions"/>. The wire format is not ours to choose, so these
/// tests pin the two conversions the API depends on: snake case property names and lower case
/// enum names.
/// </summary>
[TestClass]
public sealed class JsonSerializationTests
{
	/// <summary>
	/// Enums have to go out as names rather than the numbers the default converter would emit.
	/// </summary>
	[TestMethod]
	public void MessageRoleSerializesAsALowerCaseName()
	{
		Message message = new()
		{
			Role = MessageRole.Developer,
			Content = [new() { Type = MessageContentType.Text, Text = "hello" }],
		};

		string json = JsonSerializer.Serialize(message, Json.SerializerOptions);

		using JsonDocument document = JsonDocument.Parse(json);
		Assert.AreEqual("developer", document.RootElement.GetProperty("role").GetString());
	}

	/// <summary>
	/// Content entries carry their own enum, and the text rides along unchanged.
	/// </summary>
	[TestMethod]
	public void MessageContentSerializesItsTypeAndText()
	{
		Message message = new()
		{
			Role = MessageRole.User,
			Content = [new() { Type = MessageContentType.Text, Text = "hello" }],
		};

		string json = JsonSerializer.Serialize(message, Json.SerializerOptions);

		using JsonDocument document = JsonDocument.Parse(json);
		JsonElement content = document.RootElement.GetProperty("content");
		Assert.AreEqual(1, content.GetArrayLength());
		Assert.AreEqual("text", content[0].GetProperty("type").GetString());
		Assert.AreEqual("hello", content[0].GetProperty("text").GetString());
	}

	/// <summary>
	/// Multi word property names are the ones the naming policy actually changes, so they are the
	/// ones worth pinning. A regression here would be accepted by the API and quietly ignored.
	/// </summary>
	[TestMethod]
	public void MultiWordPropertyNamesSerializeAsSnakeCase()
	{
		Response response = new()
		{
			Summary = "did the thing",
			CommitMessage = "feat: did the thing",
			Files = [new() { FilePath = "src/Program.cs", Purpose = "C# Code File", Contents = "// code" }],
		};

		string json = JsonSerializer.Serialize(response, Json.SerializerOptions);

		using JsonDocument document = JsonDocument.Parse(json);
		Assert.AreEqual("feat: did the thing", document.RootElement.GetProperty("commit_message").GetString());
		Assert.AreEqual("src/Program.cs", document.RootElement.GetProperty("files")[0].GetProperty("file_path").GetString());
	}

	/// <summary>
	/// The request body names the model, carries the conversation, and asks for a structured reply.
	/// </summary>
	[TestMethod]
	public void RequestBodySerializesTheModelMessagesAndResponseFormat()
	{
		RequestBody requestBody = new()
		{
			Model = "gpt-4o",
			Messages =
			[
				new()
				{
					Role = MessageRole.Developer,
					Content = [new() { Type = MessageContentType.Text, Text = OAI.DeveloperPrompt }],
				},
			],
		};

		string json = JsonSerializer.Serialize(requestBody, Json.SerializerOptions);

		using JsonDocument document = JsonDocument.Parse(json);
		Assert.AreEqual("gpt-4o", document.RootElement.GetProperty("model").GetString());
		Assert.AreEqual(1, document.RootElement.GetProperty("messages").GetArrayLength());
		Assert.AreEqual("json_schema", document.RootElement.GetProperty("response_format").GetProperty("type").GetString());
	}

	/// <summary>
	/// The schema has to go out as a schema. It is built by NJsonSchema, whose objects hold a
	/// parent reference on every property, so reflecting over one walks a cycle and throws. That
	/// threw inside <c>Request.Send</c>, before the request was ever made, which meant no request
	/// could succeed. This asserts the schema arrives as JSON describing the reply we want.
	/// </summary>
	[TestMethod]
	public void ResponseFormatCarriesTheSchemaAsJsonRatherThanAnObjectGraph()
	{
		RequestBody requestBody = new() { Model = "gpt-4o" };

		string json = JsonSerializer.Serialize(requestBody, Json.SerializerOptions);

		using JsonDocument document = JsonDocument.Parse(json);
		JsonElement schema = document.RootElement.GetProperty("response_format").GetProperty("schema");
		Assert.AreEqual(JsonValueKind.Object, schema.ValueKind);
		JsonElement properties = schema.GetProperty("properties");
		Assert.IsTrue(properties.TryGetProperty("Summary", out _));
		Assert.IsTrue(properties.TryGetProperty("Files", out _));
		Assert.IsTrue(properties.TryGetProperty("CommitMessage", out _));
	}

	/// <summary>
	/// Replies are read with case insensitivity on, so a payload that does not match the casing we
	/// send still binds rather than silently leaving every property at its default.
	/// </summary>
	[TestMethod]
	public void DeserializationIgnoresPropertyNameCasing()
	{
		const string json = """
			{
				"Summary": "did the thing",
				"COMMIT_MESSAGE": "feat: did the thing",
				"Files": [{ "File_Path": "src/Program.cs", "Purpose": "C# Code File", "Contents": "// code" }]
			}
			""";

		Response? response = JsonSerializer.Deserialize<Response>(json, Json.SerializerOptions);

		Assert.IsNotNull(response);
		Assert.AreEqual("did the thing", response.Summary);
		Assert.AreEqual("feat: did the thing", response.CommitMessage);
		Assert.AreEqual(1, response.Files.Count);
		Assert.AreEqual("src/Program.cs", response.Files[0].FilePath);
	}

	/// <summary>
	/// Enum names come back as well as going out, so a reply naming a role is understood.
	/// </summary>
	[TestMethod]
	public void EnumNamesDeserializeBackToTheirValues()
	{
		const string json = """{ "role": "assistant", "content": [{ "type": "text", "text": "hello" }] }""";

		Message? message = JsonSerializer.Deserialize<Message>(json, Json.SerializerOptions);

		Assert.IsNotNull(message);
		Assert.AreEqual(MessageRole.Assistant, message.Role);
		Assert.AreEqual(MessageContentType.Text, message.Content[0].Type);
	}

	/// <summary>
	/// A full round trip preserves every field, which is the property the two directions have to
	/// share for the request and reply halves to agree.
	/// </summary>
	[TestMethod]
	public void ResponseSurvivesARoundTrip()
	{
		Response original = new()
		{
			Summary = "did the thing",
			CommitMessage = "feat: did the thing",
			Files = [new() { FilePath = "src/Program.cs", Purpose = "C# Code File", Contents = "// code" }],
		};

		string json = JsonSerializer.Serialize(original, Json.SerializerOptions);
		Response? roundTripped = JsonSerializer.Deserialize<Response>(json, Json.SerializerOptions);

		Assert.IsNotNull(roundTripped);
		Assert.AreEqual(original.Summary, roundTripped.Summary);
		Assert.AreEqual(original.CommitMessage, roundTripped.CommitMessage);
		Assert.AreEqual(original.Files.Count, roundTripped.Files.Count);
		Assert.AreEqual(original.Files[0].FilePath, roundTripped.Files[0].FilePath);
		Assert.AreEqual(original.Files[0].Purpose, roundTripped.Files[0].Purpose);
		Assert.AreEqual(original.Files[0].Contents, roundTripped.Files[0].Contents);
	}
}
