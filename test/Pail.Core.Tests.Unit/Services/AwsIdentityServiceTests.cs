using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Pail.Models;
using Pail.Services;

namespace Pail.Core.Tests.Unit.Services;

public sealed class AwsIdentityServiceTests
{
	private readonly IAwsClientFactory _awsClientFactory = Substitute.For<IAwsClientFactory>();
	private readonly IAmazonSecurityTokenService _stsClient = Substitute.For<IAmazonSecurityTokenService>();
	private readonly IAmazonIdentityManagementService _iamClient = Substitute.For<IAmazonIdentityManagementService>();
	private readonly AwsDefaultChainCredentials _credentials = new("prod", "eu-west-1");

	public AwsIdentityServiceTests()
	{
		_awsClientFactory.CreateSecurityTokenServiceClient(_credentials).Returns(_stsClient);
		_awsClientFactory.CreateIdentityManagementServiceClient(_credentials).Returns(_iamClient);
	}

	[Fact]
	internal async Task TryGetCallerIdentityAsync_ReturnsAccountIdAndCallerArn()
	{
		// Arrange
		_stsClient
			.GetCallerIdentityAsync(Arg.Any<GetCallerIdentityRequest>(), Arg.Any<CancellationToken>())
			.Returns(
				new GetCallerIdentityResponse
				{
					Account = "123456789012",
					Arn = "arn:aws:sts::123456789012:assumed-role/Admin/essam",
				});

		var service = CreateService();

		// Act
		var identity = await service.TryGetCallerIdentityAsync(_credentials);

		// Assert
		Assert.Equal(new AwsCallerIdentity("123456789012", "arn:aws:sts::123456789012:assumed-role/Admin/essam"), identity);
		await _stsClient.Received(1).GetCallerIdentityAsync(Arg.Any<GetCallerIdentityRequest>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	internal async Task TryGetCallerIdentityAsync_Failure_ReturnsNull()
	{
		// Arrange
		_stsClient
			.GetCallerIdentityAsync(Arg.Any<GetCallerIdentityRequest>(), Arg.Any<CancellationToken>())
			.Throws(new AmazonServiceException("STS unavailable"));

		var service = CreateService();

		// Act
		var identity = await service.TryGetCallerIdentityAsync(_credentials);

		// Assert
		Assert.Null(identity);
	}

	[Fact]
	internal async Task TryGetAccountAliasAsync_AliasExists_ReturnsFirstAlias()
	{
		// Arrange
		_iamClient
			.ListAccountAliasesAsync(Arg.Any<ListAccountAliasesRequest>(), Arg.Any<CancellationToken>())
			.Returns(
				new ListAccountAliasesResponse
				{
					AccountAliases = ["production", "ignored"],
				});

		var service = CreateService();

		// Act
		var alias = await service.TryGetAccountAliasAsync(_credentials);

		// Assert
		Assert.Equal("production", alias);
	}

	[Fact]
	internal async Task TryGetAccountAliasAsync_NoAliasExists_ReturnsNull()
	{
		// Arrange
		_iamClient
			.ListAccountAliasesAsync(Arg.Any<ListAccountAliasesRequest>(), Arg.Any<CancellationToken>())
			.Returns(new ListAccountAliasesResponse { AccountAliases = [] });

		var service = CreateService();

		// Act
		var alias = await service.TryGetAccountAliasAsync(_credentials);

		// Assert
		Assert.Null(alias);
	}

	[Fact]
	internal async Task TryGetAccountAliasAsync_Failure_ReturnsNull()
	{
		// Arrange
		_iamClient
			.ListAccountAliasesAsync(Arg.Any<ListAccountAliasesRequest>(), Arg.Any<CancellationToken>())
			.Throws(new AmazonIdentityManagementServiceException("Access denied"));

		var service = CreateService();

		// Act
		var alias = await service.TryGetAccountAliasAsync(_credentials);

		// Assert
		Assert.Null(alias);
	}

	private AwsIdentityService CreateService() => new(_awsClientFactory);
}
