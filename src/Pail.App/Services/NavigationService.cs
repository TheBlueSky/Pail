using Pail.Services;

namespace Pail.App.Services;

public sealed class NavigationService : INavigationService
{
	private Frame? _frame;

	public void Initialize(Frame frame) => _frame = frame;

	public void NavigateTo(string pageKey, object? parameter = null)
	{
		if (_frame is null)
		{
			return;
		}

		var pageType = pageKey switch
		{
			"LoginPage" => typeof(LoginPage),
			"BucketListPage" => typeof(BucketListPage),
			"ObjectBrowserPage" => typeof(ObjectBrowserPage),
			_ => null
		};

		if (pageType is not null)
		{
			_frame.Navigate(pageType, parameter);
		}
	}

	public void GoBack()
	{
		if (_frame?.CanGoBack is true)
		{
			_frame.GoBack();
		}
	}
}
