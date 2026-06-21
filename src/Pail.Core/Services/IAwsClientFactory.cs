using Amazon.IdentityManagement;
using Amazon.S3;
using Amazon.SecurityToken;
using Pail.Models;

namespace Pail.Services;

public interface IAwsClientFactory
{
	public IAmazonS3 CreateS3Client(IAwsCredentials credentials);

	public IAmazonSecurityTokenService CreateSecurityTokenServiceClient(IAwsCredentials credentials);

	public IAmazonIdentityManagementService CreateIdentityManagementServiceClient(IAwsCredentials credentials);
}
