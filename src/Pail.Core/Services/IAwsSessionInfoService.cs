using Pail.Models;

namespace Pail.Services;

public interface IAwsSessionInfoService
{
	public AwsSessionInfo? Current { get; }

	public void SetCurrent(AwsSessionInfo sessionInfo);

	public void Clear();
}
