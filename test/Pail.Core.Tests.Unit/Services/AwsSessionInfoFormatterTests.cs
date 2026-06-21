using Pail.Models;
using Pail.Services;

namespace Pail.Core.Tests.Unit.Services;

public sealed class AwsSessionInfoFormatterTests
{
	[Theory]
	[InlineData("arn:aws:sts::123456789012:assumed-role/Admin/essam", "Admin/essam")]
	[InlineData("arn:aws:iam::123456789012:role/path/Admin", "path/Admin")]
	[InlineData("arn:aws:iam::123456789012:user/essam", "essam")]
	[InlineData("arn:aws:sts::123456789012:federated-user/essam", "federated-user/essam")]
	internal void FormatSummary_AllFieldsAvailable_JoinsFieldsAndShortensAssumedRoleArn(string callerArn, string expected)
	{
		// Arrange
		var sessionInfo = new AwsSessionInfo(
			"eu-west-1",
			"prod-profile",
			"123456789012",
			callerArn,
			"production");

		// Act
		var summary = AwsSessionInfoFormatter.FormatSummary(sessionInfo);

		// Assert
		Assert.Equal($"production | prod-profile | 123456789012 | eu-west-1 | {expected}", summary);
	}

	[Fact]
	internal void FormatSummary_MissingOptionalFields_OmitsEmptySeparators()
	{
		// Arrange
		var sessionInfo = new AwsSessionInfo("us-west-2", null, null, null, null);

		// Act
		var summary = AwsSessionInfoFormatter.FormatSummary(sessionInfo);

		// Assert
		Assert.Equal("us-west-2", summary);
	}
}
