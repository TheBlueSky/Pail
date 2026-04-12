using NSubstitute;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class ObjectBrowserViewModelTests
{
	private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
	private readonly IClipboardService _clipboardService = Substitute.For<IClipboardService>();
	private readonly IStatusMessageService _statusMessageService = Substitute.For<IStatusMessageService>();

	[Fact]
	internal async Task CopyObjectNameCommand_SelectedItem_CopiesAndShowsSuccessMessage()
	{
		// Arrange
		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _clipboardService, _statusMessageService)
		{
			SelectedItem = new S3ObjectItem
			{
				Name = "report.csv",
				Key = "reports/2026/report.csv",
				IsFolder = false,
			},
		};

		_clipboardService.CopyTextAsync("report.csv").Returns(true);

		// Act
		await viewModel.CopyObjectNameCommand.ExecuteAsync(null);

		// Assert
		await _clipboardService.Received(1).CopyTextAsync("report.csv");
		_statusMessageService.Received(1).ShowInfo("Copied object name: report.csv");
	}

	[Fact]
	internal async Task CopyObjectFullKeyCommand_SelectedItem_CopiesAndShowsSuccessMessage()
	{
		// Arrange
		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _clipboardService, _statusMessageService)
		{
			SelectedItem = new S3ObjectItem
			{
				Name = "report.csv",
				Key = "reports/2026/report.csv",
				IsFolder = false,
			},
		};

		_clipboardService.CopyTextAsync("reports/2026/report.csv").Returns(true);

		// Act
		await viewModel.CopyObjectFullKeyCommand.ExecuteAsync(null);

		// Assert
		await _clipboardService.Received(1).CopyTextAsync("reports/2026/report.csv");
		_statusMessageService.Received(1).ShowInfo("Copied full key: reports/2026/report.csv");
	}

	[Fact]
	internal async Task CopyCommands_NoSelection_DoNotCopy()
	{
		// Arrange
		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _clipboardService, _statusMessageService);

		// Act
		await viewModel.CopyObjectNameCommand.ExecuteAsync(null);
		await viewModel.CopyObjectFullKeyCommand.ExecuteAsync(null);

		// Assert
		await _clipboardService.DidNotReceive().CopyTextAsync(Arg.Any<string>());
		_statusMessageService.DidNotReceive().ShowInfo(Arg.Any<string>());
	}
}
