using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Pail.Models;
using Pail.ViewModels;
using Windows.System;
using Windows.UI.Core;
using WinUI.TableView;

namespace Pail.App.Views;

public sealed partial class ObjectBrowserPage : Page
{
	private int? _keyboardNavigationRowIndex;

	public ObjectBrowserPage()
	{
		InitializeComponent();

		ViewModel = PailApp.Services.GetRequiredService<ObjectBrowserViewModel>();

		ObjectGrid.PointerPressed += OnGridPointerPressed;
		ObjectGrid.SelectionChanged += OnGridSelectionChanged;
	}

	public ObjectBrowserViewModel ViewModel { get; }

	protected override async void OnNavigatedTo(NavigationEventArgs e)
	{
		base.OnNavigatedTo(e);

		if (e.Parameter is string bucketName)
		{
			await ViewModel.InitializeAsync(bucketName);
		}
	}

	private void OnGridCellDoubleTapped(object sender, TableViewCellDoubleTappedEventArgs e)
	{
		if (e.Item is S3ObjectItem item)
		{
			ViewModel.OpenItemCommand.Execute(item);
		}
	}

	private void OnGridRowContextFlyoutOpening(object sender, TableViewRowContextFlyoutEventArgs e)
	{
		if (e.Item is S3ObjectItem item && ObjectGrid.SelectedItems.Contains(item) is false)
		{
			ObjectGrid.SelectedItems.Clear();
			ObjectGrid.SelectedItems.Add(item);
		}
	}

	// TableView's built-in Copy command is triggered by Ctrl+C and Ctrl+Shift+C.
	// We handle it to provide custom copy behavior for object names and full keys.
	private void OnGridCopyToClipboard(object sender, TableViewCopyToClipboardEventArgs e)
	{
		if (e.IncludeHeaders) // Ctrl+Shift+C
		{
			CopyFullKeys();
		}
		else // Ctrl+C
		{
			CopyNames();
		}

		e.Handled = true;
	}

	private void OnCopyNameClick(object sender, RoutedEventArgs e) =>
		CopyNames();

	private void OnCopyFullKeyClick(object sender, RoutedEventArgs e) =>
		CopyFullKeys();

	private void OnDownloadClick(object sender, RoutedEventArgs e)
	{
		var selected = ObjectGrid.SelectedItems.Cast<S3ObjectItem>().ToList();
		ViewModel.DownloadSelectedCommand.Execute(selected);
	}

	private void OnCopyObjectNameContextClick(object sender, RoutedEventArgs e) =>
		CopyNames();

	private void OnCopyObjectFullKeyContextClick(object sender, RoutedEventArgs e) =>
		CopyFullKeys();

	// Intercept app-specific shortcuts before TableView handles its built-in keyboard commands.
	private async void OnGridPreviewKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key == VirtualKey.Left && IsModifierKeyDown(VirtualKey.Menu))
		{
			await ViewModel.GoBackCommand.ExecuteAsync(null);

			e.Handled = true;
			return;
		}

		if (ViewModel.Items.Count == 0 || IsModifierKeyDown(VirtualKey.Control) || IsModifierKeyDown(VirtualKey.Shift))
		{
			return;
		}

		if (e.Key == VirtualKey.Enter)
		{
			if (GetSingleSelectedItem() is S3ObjectItem item)
			{
				ViewModel.OpenItemCommand.Execute(item);
			}

			e.Handled = true;
			return;
		}

		if (e.Key is VirtualKey.Home or VirtualKey.End)
		{
			var targetIndex = e.Key == VirtualKey.Home ? 0 : ViewModel.Items.Count - 1;
			e.Handled = await SelectRowAsync(targetIndex);
			return;
		}

		if (_keyboardNavigationRowIndex is not int currentIndex || e.Key is not (VirtualKey.Up or VirtualKey.Down))
		{
			return;
		}

		var nextIndex = e.Key == VirtualKey.Up
			? Math.Max(0, currentIndex - 1)
			: Math.Min(ViewModel.Items.Count - 1, currentIndex + 1);

		e.Handled = await SelectRowAsync(nextIndex);

		S3ObjectItem? GetSingleSelectedItem()
		{
			return ObjectGrid.SelectedItems.Count == 1
				? ObjectGrid.SelectedItems[0] as S3ObjectItem
				: null;
		}

		async Task<bool> SelectRowAsync(int targetIndex)
		{
			if (targetIndex < 0 || targetIndex >= ViewModel.Items.Count)
			{
				return false;
			}

			ObjectGrid.SelectedIndex = targetIndex;
			_keyboardNavigationRowIndex = targetIndex;

			var row = await ObjectGrid.ScrollRowIntoView(targetIndex);
			row?.Focus(FocusState.Programmatic);

			return true;
		}
	}

	private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
	{
		if (sender is TextBox searchTextBox)
		{
			ViewModel.SearchText = searchTextBox.Text ?? string.Empty;
		}
	}

	private void OnPageKeyboardAcceleratorInvoked(KeyboardAccelerator accelerator, KeyboardAcceleratorInvokedEventArgs args)
	{
		if (accelerator == SearchKeyboardAcceleratorF || accelerator == SearchKeyboardAcceleratorF3)
		{
			SearchTextBox.Focus(FocusState.Programmatic);
			args.Handled = true;
		}
	}

	private void OnGridPointerPressed(object sender, PointerRoutedEventArgs e) =>
		_keyboardNavigationRowIndex = null;

	private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_keyboardNavigationRowIndex.HasValue)
		{
			_keyboardNavigationRowIndex = ObjectGrid.SelectedIndex >= 0 ? ObjectGrid.SelectedIndex : null;
		}
	}

	private void CopyNames()
	{
		var selected = ObjectGrid.SelectedItems.Cast<S3ObjectItem>().ToList();
		ViewModel.CopyObjectNameCommand.Execute(selected);
	}

	private void CopyFullKeys()
	{
		var selected = ObjectGrid.SelectedItems.Cast<S3ObjectItem>().ToList();
		ViewModel.CopyObjectFullKeyCommand.Execute(selected);
	}

	private static bool IsModifierKeyDown(VirtualKey key)
	{
		var state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
		return state is CoreVirtualKeyStates.Down or (CoreVirtualKeyStates.Down | CoreVirtualKeyStates.Locked);
	}
}
