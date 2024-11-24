# Velopack.GoogleDrive

![Nuget](https://img.shields.io/nuget/v/Velopack.GoogleDrive?style=flat-square) 
![Build Status](https://img.shields.io/github/actions/workflow/status/velopack/velopack/main.yml?branch=main&style=flat-square)
![License](https://img.shields.io/github/license/your-repo/your-project?style=flat-square)

**Velopack.GoogleDrive** is an extension for [Velopack](https://github.com/velopack/velopack) that that allows you to use Google Drive as an update source for Velopack packages. This library enables you to store and manage application updates in a [Google Drive folder](https://drive.google.com).

---

## 🌟 Features

- 🗂 Supports updates from Google Drive folders.
- 🔄 Handles both full and delta update packages.
- 🛠 Integrates with `ILogger` for logging and monitoring.
- 🔌 Implements `IUpdateSource` for seamless integration with Velopack.

---

## 🚀 Installation

### Install via NuGet
Run the following command in your project:

```bash
dotnet add package Velopack.GoogleDrive
```

Or add this to your `.csproj` file:

```xml
<PackageReference Include="Velopack.GoogleDrive" Version="1.0.0" />
```

---

## 📖 Usage

### Configure `GoogleDriveUpdateSource`
Here is an example of how to configure `GoogleDriveUpdateSource`:

```csharp
using Microsoft.Extensions.Logging;
using Velopack;

var folderPath = "https://drive.google.com/drive/folders/YOUR_FOLDER_ID";
var apiKey = "YOUR_GOOGLE_API_KEY"; // [Get Google Drive API key](https://developers.google.com/drive/api)
var packageId = "com.example.myapp";

var logger = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
}).CreateLogger<GoogleDriveUpdateSource>();

var source = new GoogleDriveUpdateSource(
    folderPath: folderPath,
    apiKey: apiKey,
    packageId: packageId,
    logger: logger,
    options: new GoogleDriveUpdateSource.Options
    {
        ApplicationName = "MyAppUpdater"
    });
```

### Retrieve Available Updates
To retrieve a list of available updates:

```csharp
var releaseFeed = await source.GetReleaseFeed(logger, channel: "stable");
foreach (var asset in releaseFeed.Assets)
{
    Console.WriteLine($"Found package: {asset.FileName} (version {asset.Version})");
}
```

### Download and Save Packages
To download a selected package to a local file:

```csharp
var asset = releaseFeed.Assets.First();
await source.DownloadReleaseEntry(
    logger,
    releaseEntry: asset,
    localFile: "update.nupkg",
    progress: percent => Console.WriteLine($"Progress: {percent}%")
);
```

---

## 🛠 Requirements
- .NET 8.0 or later
- A Google account and a Google Drive API key
- Velopack

---

## ✅ Running Tests
To run unit tests:

```bash
dotnet test
```

---

## 🤝 Contributing
If you’d like to contribute to this project:

1. Fork the repository.
2. Create a new branch (`git checkout -b my-feature`).
3. Make your changes.
4. Submit a Pull Request.

We welcome all contributions! 😊

---

## 📜 License
This project is licensed under the MIT License. See the LICENSE file for details.

---

## 📧 Contact
If you have questions or need help, feel free to reach out via GitHub Issues.
