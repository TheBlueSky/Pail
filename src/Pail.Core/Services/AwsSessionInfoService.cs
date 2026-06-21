using Pail.Models;

namespace Pail.Services;

public sealed class AwsSessionInfoService : IAwsSessionInfoService
{
	public AwsSessionInfo? Current { get; private set; }

	public void SetCurrent(AwsSessionInfo sessionInfo)
	{
		ArgumentNullException.ThrowIfNull(sessionInfo);

		Current = sessionInfo;
	}

	public void Clear() => Current = null;
}
