// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.OAICLI;

/// <summary>
/// Represents a message in the conversation.
/// </summary>
internal class Message
{
	/// <summary>
	/// Gets or sets the role of the message sender.
	/// </summary>
	public MessageRole Role { get; set; }

	/// <summary>
	/// Gets or sets the content of the message.
	/// </summary>
	public MessageContent[] Content { get; set; } = [];
}

/// <summary>
/// Represents the content of a message.
/// </summary>
internal class MessageContent
{
	/// <summary>
	/// Gets or sets the type of the message content.
	/// </summary>
	public MessageContentType Type { get; set; }

	/// <summary>
	/// Gets or sets the text of the message content.
	/// </summary>
	public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Specifies the content types that can be used in messages.
/// </summary>
internal enum MessageContentType
{
	Text,
}

/// <summary>
/// Specifies the roles that can be assigned to messages.
/// </summary>
internal enum MessageRole
{
	Developer,
	User,
	Assistant,
}
