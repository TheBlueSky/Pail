namespace Pail.App.Services;

public sealed class NavigationService : INavigationHostService
{
	private Frame? _rootFrame;
	private Frame? _contentFrame;

	public bool CanGoBack => _contentFrame?.CanGoBack is true || _rootFrame?.CanGoBack is true;

	public void Initialize(Frame rootFrame) => _rootFrame = rootFrame;

	public void RegisterContentFrame(Frame contentFrame) => _contentFrame = contentFrame;

	public void NavigateTo(string pageKey, object? parameter = null, bool clearBackStack = false)
	{
		var frame = GetTargetFrame(pageKey);

		if (frame is null)
		{
			return;
		}

		var pageType = pageKey switch
		{
			"LoginPage" => typeof(LoginPage),
			"MainPage" => typeof(MainPage),
			"BucketListPage" => typeof(BucketListPage),
			"SettingsPage" => typeof(SettingsPage),
			"ObjectBrowserPage" => typeof(ObjectBrowserPage),
			_ => null
		};

		if (pageType is not null)
		{
			frame.Navigate(pageType, parameter);

			if (clearBackStack)
			{
				frame.BackStack.Clear();
			}
		}
	}

	public void GoBack()
	{
		if (_contentFrame?.CanGoBack is true)
		{
			_contentFrame.GoBack();
			return;
		}

		if (_rootFrame?.CanGoBack is true)
		{
			_rootFrame.GoBack();
		}
	}

	private Frame? GetTargetFrame(string pageKey) => pageKey switch
	{
		"LoginPage" or "MainPage" => _rootFrame,
		_ => _contentFrame ?? _rootFrame,
	};
}
