using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using Pail.Models;
using Pail.ViewModels;

namespace Pail.App.Views;

public sealed partial class BucketListPage : Page
{
	public BucketListPage()
	{
		InitializeComponent();

		ViewModel = PailApp.Services.GetRequiredService<BucketListViewModel>();
	}

	public BucketListViewModel ViewModel { get; }

	protected override async void OnNavigatedTo(NavigationEventArgs e)
	{
		base.OnNavigatedTo(e);

		await ViewModel.LoadBucketsAsync();
	}

	private void OnBucketClick(object sender, ItemClickEventArgs e)
	{
		if (e.ClickedItem is S3BucketItem bucket)
		{
			ViewModel.SelectBucketCommand.Execute(bucket);
		}
	}
}
