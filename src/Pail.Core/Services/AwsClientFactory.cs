using Amazon;
using Amazon.IdentityManagement;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SecurityToken;
using Pail.Models;

namespace Pail.Services;

public sealed class AwsClientFactory : IAwsClientFactory
{
	public IAmazonSecurityTokenService CreateSecurityTokenServiceClient(IAwsCredentials credentials)
	{
		ArgumentNullException.ThrowIfNull(credentials);

		var region = RegionEndpoint.GetBySystemName(credentials.Region);
		var awsCredentials = GetAwsCredentials(credentials);

		return awsCredentials is null
			? new AmazonSecurityTokenServiceClient(region)
			: new AmazonSecurityTokenServiceClient(awsCredentials, region);
	}

	public IAmazonIdentityManagementService CreateIdentityManagementServiceClient(IAwsCredentials credentials)
	{
		ArgumentNullException.ThrowIfNull(credentials);

		var awsCredentials = GetAwsCredentials(credentials);

		return awsCredentials is null
			? new AmazonIdentityManagementServiceClient()
			: new AmazonIdentityManagementServiceClient(awsCredentials);
	}

	private static AWSCredentials? GetAwsCredentials(IAwsCredentials credentials)
	{
		if (credentials is AwsSessionCredentials sessionCredentials)
		{
			return string.IsNullOrEmpty(sessionCredentials.SessionToken)
				? new BasicAWSCredentials(sessionCredentials.AccessKey, sessionCredentials.SecretKey)
				: new SessionAWSCredentials(sessionCredentials.AccessKey, sessionCredentials.SecretKey, sessionCredentials.SessionToken);
		}

		if (credentials is AwsDefaultChainCredentials defaultChainCredentials)
		{
			return string.IsNullOrWhiteSpace(defaultChainCredentials.ProfileName)
				? null
				: GetProfileCredentials(defaultChainCredentials.ProfileName);
		}

		throw new ArgumentOutOfRangeException(nameof(credentials), credentials, "Unsupported AWS credential type.");
	}

	private static AWSCredentials GetProfileCredentials(string profileName)
	{
		var profileStore = new CredentialProfileStoreChain();

		return profileStore.TryGetAWSCredentials(profileName, out var credentials)
			? credentials
			: throw new InvalidOperationException($"AWS profile '{profileName}' was not found or could not be loaded.");
	}
}
