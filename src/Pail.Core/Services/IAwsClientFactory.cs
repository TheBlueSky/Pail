using Amazon.IdentityManagement;
using Amazon.SecurityToken;
using Pail.Models;

namespace Pail.Services;

public interface IAwsClientFactory
{
	public IAmazonSecurityTokenService CreateSecurityTokenServiceClient(IAwsCredentials credentials);

	public IAmazonIdentityManagementService CreateIdentityManagementServiceClient(IAwsCredentials credentials);
}
