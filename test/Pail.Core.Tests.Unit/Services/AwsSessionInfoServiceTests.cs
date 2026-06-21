using Pail.Models;
using Pail.Services;

namespace Pail.Core.Tests.Unit.Services;

public sealed class AwsSessionInfoServiceTests
{
	[Fact]
	internal void SetCurrent_StoresSessionInfo()
	{
		// Arrange
		var service = new AwsSessionInfoService();
		var sessionInfo = new AwsSessionInfo("eu-west-1", "prod", "123456789012", "arn:aws:sts::123456789012:assumed-role/Admin/essam", null);

		// Act
		service.SetCurrent(sessionInfo);

		// Assert
		Assert.Equal(sessionInfo, service.Current);
	}

	[Fact]
	internal void Clear_CurrentSessionExists_ClearsSession()
	{
		// Arrange
		var service = new AwsSessionInfoService();
		service.SetCurrent(new AwsSessionInfo("eu-west-1", "prod", "123456789012", "arn:aws:sts::123456789012:assumed-role/Admin/essam", "production"));

		// Act
		service.Clear();

		// Assert
		Assert.Null(service.Current);
	}

	[Fact]
	internal void Clear_NoCurrentSession_LeavesCurrentNull()
	{
		// Arrange
		var service = new AwsSessionInfoService();

		// Act
		service.Clear();

		// Assert
		Assert.Null(service.Current);
	}
}
