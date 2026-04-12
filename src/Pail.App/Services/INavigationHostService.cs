using Pail.Services;

namespace Pail.App.Services;

public interface INavigationHostService : INavigationService
{
	public void Initialize(Frame rootFrame);

	public void RegisterContentFrame(Frame contentFrame);
}
