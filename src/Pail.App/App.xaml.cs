using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Navigation;
using Pail.App.Services;
using Pail.Services;
using Pail.ViewModels;
using Windows.Graphics;
using WinRT.Interop;

namespace Pail.App;

public partial class PailApp : Application
{
	private Window? _window;

	public PailApp()
	{
		InitializeComponent();
	}

	public static IServiceProvider Services { get; private set; } = ConfigureServices();

	private static ServiceProvider ConfigureServices()
	{
		var services = new ServiceCollection();

		// Services
		services.AddSingleton<IAwsProfileService, AwsProfileService>();
		services.AddSingleton<IClipboardService, ClipboardService>();
		services.AddSingleton<ICopyActionService, CopyActionService>();
		services.AddSingleton<INavigationService, NavigationService>();
		services.AddSingleton<IS3Service, S3Service>();
		services.AddSingleton<ISettingsService, SettingsService>();
		services.AddSingleton<IStatusMessageService, StatusMessageService>();

		// ViewModels
		services.AddTransient<BucketListViewModel>();
		services.AddTransient<LoginViewModel>();
		services.AddTransient<ObjectBrowserViewModel>();

		return services.BuildServiceProvider();
	}

	protected override async void OnLaunched(LaunchActivatedEventArgs e)
	{
		await Services.GetRequiredService<ISettingsService>().LoadAsync();

		_window = new Window { Title = "Pail – AWS S3 Browser" };

		if (_window.Content is not Frame rootFrame)
		{
			rootFrame = new Frame();
			rootFrame.NavigationFailed += OnNavigationFailed;

			_window.Content = rootFrame;
		}

		SetWindowIcon(_window);
		CenterWindow(_window);

		// Register the frame with the navigation service
		var navService = (NavigationService)Services.GetRequiredService<INavigationService>();
		navService.Initialize(rootFrame);

		navService.NavigateTo("LoginPage");

		_window.Activate();
	}

	private static void SetWindowIcon(Window window)
	{
		var appWindow = GetAppWindow(window);
		var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Pail.ico");

		if (File.Exists(iconPath))
		{
			appWindow.SetIcon(iconPath);
		}
	}

	private static void CenterWindow(Window window)
	{
		var appWindow = GetAppWindow(window);
		var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
		var workArea = displayArea.WorkArea;
		var size = appWindow.Size;
		var x = workArea.X + Math.Max(0, (workArea.Width - size.Width) / 2);
		var y = workArea.Y + Math.Max(0, (workArea.Height - size.Height) / 2);

		appWindow.Move(new PointInt32(x, y));
	}

	private static AppWindow GetAppWindow(Window window)
	{
		var hwnd = WindowNative.GetWindowHandle(window);
		var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
		return AppWindow.GetFromWindowId(windowId);
	}

	private void OnNavigationFailed(object sender, NavigationFailedEventArgs e) =>
		throw new Exception($"Failed to load Page {e.SourcePageType.FullName}.");
}
