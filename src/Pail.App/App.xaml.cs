using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Navigation;
using Pail.App.Services;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;
using Windows.Graphics;
using WinRT.Interop;

namespace Pail.App;

public partial class PailApp : Application
{
	private const double StatusOverlayInset = 24;

	private Window? _window;
	private Frame? _rootFrame;

	public PailApp()
	{
		InitializeComponent();
	}

	public static Window? MainWindow { get; private set; }

	public static IServiceProvider Services { get; private set; } = ConfigureServices();

	private static ServiceProvider ConfigureServices()
	{
		var services = new ServiceCollection();

		var settingsConfiguration = CreateSettingsConfiguration();
		services.AddSingleton<IConfiguration>(settingsConfiguration);
		services.AddOptions<AppSettings>().Bind(settingsConfiguration);

		// Services
		services.AddSingleton<IAwsProfileService, AwsProfileService>();
		services.AddSingleton<IAppThemeService, AppThemeService>();
		services.AddSingleton<IAwsConsoleCredentialsParser, AwsConsoleCredentialsParser>();
		services.AddSingleton<IClipboardService, ClipboardService>();
		services.AddSingleton<ICopyActionService, CopyActionService>();
		services.AddSingleton<IFolderPickerService, FolderPickerService>();
		services.AddSingleton<ILocalizationService, ResourceLocalizationService>();
		services.AddSingleton<INavigationHostService, NavigationService>();
		services.AddSingleton<INavigationService>(serviceProvider => serviceProvider.GetRequiredService<INavigationHostService>());
		services.AddSingleton<IS3Service, S3Service>();
		services.AddSingleton<ISettingsService, SettingsService>();
		services.AddSingleton<IStatusMessageService, StatusMessageService>();

		// ViewModels
		services.AddTransient<BucketListViewModel>();
		services.AddTransient<LoginViewModel>();
		services.AddTransient<ObjectBrowserViewModel>();
		services.AddTransient<SettingsViewModel>();

		return services.BuildServiceProvider();
	}

	protected override void OnLaunched(LaunchActivatedEventArgs e)
	{
		_window = new Window { Title = Services.GetRequiredService<ILocalizationService>().GetString("AppWindowTitle", "Pail – AWS S3 Browser") };
		MainWindow = _window;
		_rootFrame = CreateRootFrame();
		_window.Content = CreateShellRoot(_rootFrame);

		Services.GetRequiredService<IAppThemeService>().ApplyTheme(Services.GetRequiredService<ISettingsService>().AppTheme);

		SetWindowIcon(_window);
		CenterWindow(_window);
		GetAppWindow(_window).Closing += OnWindowClosing;

		// Register the frame with the navigation service
		var navService = Services.GetRequiredService<INavigationHostService>();
		navService.Initialize(_rootFrame);

		navService.NavigateTo("LoginPage");
		_window.Activate();
	}

	private void OnNavigationFailed(object sender, NavigationFailedEventArgs e) =>
		throw new Exception($"Failed to load Page {e.SourcePageType.FullName}.");

	private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
	{
		if (_rootFrame is null)
		{
			return;
		}

		var wasTabStop = _rootFrame.IsTabStop;
		_rootFrame.IsTabStop = true;
		_rootFrame.Focus(FocusState.Programmatic);
		_rootFrame.IsTabStop = wasTabStop;
	}

	private Frame CreateRootFrame()
	{
		var rootFrame = new Frame();
		rootFrame.NavigationFailed += OnNavigationFailed;
		return rootFrame;
	}

	private static Grid CreateShellRoot(Frame rootFrame)
	{
		var shellRoot = new Grid();
		var statusOverlayHost = new StatusOverlayHost
		{
			Margin = new Thickness(StatusOverlayInset),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
		};

		Canvas.SetZIndex(statusOverlayHost, 10);

		shellRoot.Children.Add(rootFrame);
		shellRoot.Children.Add(statusOverlayHost);

		return shellRoot;
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

	private static IConfigurationRoot CreateSettingsConfiguration()
	{
		var settingsFilePath = Path.Combine(AppContext.BaseDirectory, SettingsService.DefaultFileName);

		return new ConfigurationBuilder()
			.SetBasePath(Path.GetDirectoryName(settingsFilePath) ?? AppContext.BaseDirectory)
			.AddJsonFile(Path.GetFileName(settingsFilePath), optional: true, reloadOnChange: true)
			.Build();
	}
}
