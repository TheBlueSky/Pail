using Pail.Services;

namespace Pail.App.Services;

public class NavigationService : INavigationService
{
	private Frame? _frame;

	public void Initialize(Frame frame)
	{
		_frame = frame;
	}

	public void NavigateTo(string pageKey, object? parameter = null)
	{
		if (_frame == null)
			return;

		Type? pageType = pageKey switch
		{
			"LoginPage" => typeof(LoginPage),
			"BucketListPage" => typeof(BucketListPage),
			"ObjectBrowserPage" => typeof(ObjectBrowserPage),
			_ => null
		};

		if (pageType != null)
		{
			_frame.Navigate(pageType, parameter);
		}
	}

	public void GoBack()
	{
		if (_frame?.CanGoBack == true)
		{
			_frame.GoBack();
		}
	}
}
