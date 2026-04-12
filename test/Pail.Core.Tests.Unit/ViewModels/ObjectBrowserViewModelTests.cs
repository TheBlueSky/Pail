using NSubstitute;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class ObjectBrowserViewModelTests
{
	private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
	private readonly ICopyActionService _copyActionService = Substitute.For<ICopyActionService>();

	[Fact]
	internal async Task CopyObjectNameCommand_SelectedItem_CopiesAndShowsSuccessMessage()
	{
		// Arrange
		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _copyActionService)
		{
			SelectedItem = new S3ObjectItem
			{
				Name = "report.csv",
				Key = "reports/2026/report.csv",
				IsFolder = false,
			},
		};

		// Act
		await viewModel.CopyObjectNameCommand.ExecuteAsync(null);

		// Assert
		await _copyActionService.Received(1).CopyWithFeedbackAsync(
			"report.csv",
			"Copied object name: report.csv",
			"Failed to copy object name.");
	}

	[Fact]
	internal async Task CopyObjectFullKeyCommand_SelectedItem_CopiesAndShowsSuccessMessage()
	{
		// Arrange
		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _copyActionService)
		{
			SelectedItem = new S3ObjectItem
			{
				Name = "report.csv",
				Key = "reports/2026/report.csv",
				IsFolder = false,
			},
		};

		// Act
		await viewModel.CopyObjectFullKeyCommand.ExecuteAsync(null);

		// Assert
		await _copyActionService.Received(1).CopyWithFeedbackAsync(
			"reports/2026/report.csv",
			"Copied full key: reports/2026/report.csv",
			"Failed to copy full key.");
	}

	[Fact]
	internal async Task CopyCommands_NoSelection_DoNotCopy()
	{
		// Arrange
		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _copyActionService);

		// Act
		await viewModel.CopyObjectNameCommand.ExecuteAsync(null);
		await viewModel.CopyObjectFullKeyCommand.ExecuteAsync(null);

		// Assert
		await _copyActionService.DidNotReceive().CopyWithFeedbackAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
	}
}
