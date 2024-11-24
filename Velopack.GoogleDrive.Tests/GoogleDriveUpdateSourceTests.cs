using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using FluentAssertions;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Microsoft.Extensions.Logging;
using Velopack;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace Velopack.GoogleDrive.Tests;

public class GoogleDriveUpdateSourceTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenFolderPathIsNullOrEmpty()
    {
        // Arrange
        string invalidFolderPath = null!;
        var apiKey = "test-api-key";
        var packageId = "test-package";

        // Act
        Action act = () => new GoogleDriveUpdateSource(invalidFolderPath, apiKey, packageId);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*folderPath*");
    }

    [Fact]
    public void Constructor_ShouldInitialize_WhenParametersAreValid()
    {
        // Arrange
        var folderPath = "folder-id";
        var apiKey = "test-api-key";
        var packageId = "test-package";

        // Act
        var source = new GoogleDriveUpdateSource(folderPath, apiKey, packageId);

        // Assert
        source.Should().NotBeNull();
    }

    [Fact]
    public async Task GetReleaseFeed_ShouldThrow_WhenDisposed()
    {
        // Arrange
        var folderPath = "folder-id";
        var apiKey = "test-api-key";
        var packageId = "test-package";
        var logger = A.Fake<ILogger>();

        var source = new GoogleDriveUpdateSource(folderPath, apiKey, packageId, logger);
        source.Dispose();

        // Act
        Func<Task> act = async () => await source.GetReleaseFeed(logger, "stable");

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task ListNupkgFiles_ShouldReturnEmpty_WhenNoFilesFound()
    {
        // Arrange
        var folderPath = "folder-id";
        var apiKey = "test-api-key";
        var packageId = "test-package";
        var logger = A.Fake<ILogger>();

        // Mockowanie odpowiedzi API Google Drive
        var fakeResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{ \"files\": [] }", Encoding.UTF8, "application/json")
        };
        var fakeHandler = new FakeHttpMessageHandler(fakeResponse);
        var httpClient = new HttpClient(fakeHandler);

        // Inicjalizacja DriveService z niestandardowym HttpClient
        var driveService = new DriveService(new Google.Apis.Services.BaseClientService.Initializer
        {
            HttpClientFactory = new Google.Apis.Http.HttpClientFactory()
        });

        var source = new GoogleDriveUpdateSource(folderPath, apiKey, packageId, logger, driveService);

        // Act
        var method = source.GetType()
            .GetMethod("ListNupkgFiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task<IList<DriveFile>>)method?.Invoke(source, null)!;
        var files = await task;

        // Assert
        files.Should().NotBeNull().And.BeEmpty();
    }
}
