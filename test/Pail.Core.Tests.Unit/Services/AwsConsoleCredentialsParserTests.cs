using Pail.Models;
using Pail.Services;

namespace Pail.Core.Tests.Unit.Services;

public sealed class AwsConsoleCredentialsParserTests
{
	private const string AccessKeyId = "ASIA44VDUOCCLFCTTE4O";
	private const string SecretAccessKey = "6TPqK02zmNOBUCzINlAXGKFgiPvtBisU3O+avz/3";
	private const string SessionToken = "IQoJb3JpZ2luX2VjExample==";

	private readonly AwsConsoleCredentialsParser _parser = new();

	[Fact]
	internal void Parse_ValidAwsConsoleCredentials_ReturnsCredentialValues()
	{
		// Arrange
		const string clipboardText = $"""
			[ProfileName]
			aws_access_key_id={AccessKeyId}
			aws_secret_access_key={SecretAccessKey}
			aws_session_token={SessionToken}
			""";

		// Act
		var credentials = _parser.Parse(clipboardText);

		// Assert
		Assert.Equal(new AwsConsoleCredentials(AccessKeyId, SecretAccessKey, SessionToken), credentials);
	}

	[Fact]
	internal void Parse_WhitespaceAroundLines_ReturnsCredentialValues()
	{
		// Arrange
		const string clipboardText = $"""

			   [ProfileName]

			   aws_access_key_id = {AccessKeyId}
			   aws_secret_access_key = {SecretAccessKey}
			   aws_session_token = {SessionToken}

			""";

		// Act
		var credentials = _parser.Parse(clipboardText);

		// Assert
		Assert.Equal(new AwsConsoleCredentials(AccessKeyId, SecretAccessKey, SessionToken), credentials);
	}

	[Fact]
	internal void Parse_MissingSessionToken_ReturnsNull()
	{
		// Arrange
		const string clipboardText = $"""
			[ProfileName]
			aws_access_key_id={AccessKeyId}
			aws_secret_access_key={SecretAccessKey}
			""";

		// Act
		var credentials = _parser.Parse(clipboardText);

		// Assert
		Assert.Null(credentials);
	}

	[Fact]
	internal void Parse_DuplicateCredentialKey_ReturnsNull()
	{
		// Arrange
		const string clipboardText = $"""
			[ProfileName]
			aws_access_key_id={AccessKeyId}
			aws_access_key_id={AccessKeyId}
			aws_secret_access_key={SecretAccessKey}
			aws_session_token={SessionToken}
			""";

		// Act
		var credentials = _parser.Parse(clipboardText);

		// Assert
		Assert.Null(credentials);
	}

	[Fact]
	internal void Parse_UnexpectedExtraLine_ReturnsNull()
	{
		// Arrange
		const string clipboardText = $"""
			[ProfileName]
			aws_access_key_id={AccessKeyId}
			not_expected=true
			aws_secret_access_key={SecretAccessKey}
			aws_session_token={SessionToken}
			""";

		// Act
		var credentials = _parser.Parse(clipboardText);

		// Assert
		Assert.Null(credentials);
	}

	[Fact]
	internal void Parse_HeaderAfterCredentialLine_ReturnsNull()
	{
		// Arrange
		const string clipboardText = $"""
			aws_access_key_id={AccessKeyId}
			[ProfileName]
			aws_secret_access_key={SecretAccessKey}
			aws_session_token={SessionToken}
			""";

		// Act
		var credentials = _parser.Parse(clipboardText);

		// Assert
		Assert.Null(credentials);
	}
}
