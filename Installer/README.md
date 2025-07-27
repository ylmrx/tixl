# Building a TiXL3 Installer

## Preface

We’re using [Inno Setup](https://jrsoftware.org/isinfo.php) to generate a feature-complete `.exe` installer that includes all dependencies and installs the Windows Graphic Tools. Although not as generic as other solutions like [WiX](https://wixtoolset.org/), it was simple to set up, works out of the box, and gets the job done. In the long run, a CI/CD solution for other platforms would be ideal.

## Setup

### Build Project

1. Clone `git@github.com:TiXL3/t3.git` and switch to the `main` branch.
2. Open the project in Rider or Visual Studio.
3. Make sure you're in Release mode.
4. Rebuild the Player project first.
5. Publish the Player as a standalone folder with the following settings:

   * Target path: `Player/bin/ReleasePublished`
   * Configuration: `Release | Any CPU`
   * Target Framework: `.NET 9.0 (Windows)`
   * Deployment mode: `Self-contained`
   * Target Runtime: `win-x64`
   * Disable: `Enable ReadyToRun Compilation`, `Trim unused code`, and `Produce single file`
6. Ensure the new folder `Player/bin/ReleasePublished` exists and is approximately 200 MB in size.
7. Rebuild the solution. The result should be a valid, feature-complete build in `Editor/bin/Release/net9.0-windows/`. The build script will copy the published Player to the `Player/*` folder so it can be used later when exporting executables.

### Download Dependencies

The installer will look for the dependencies listed in `installer.iss`. As of writing, these are:

* [dotnet-sdk-9.0.203-win-x64.exe](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-9.0.203-windows-x64-installer)
* [VC\_redist.x64.exe](https://aka.ms/vs/17/release/vc_redist.x64.exe)

Download these files into the `Installer\dependencies\downloads\` folder so you end up with this structure:

```
Editor/bin/Release/net9.0-windows/

Installer/dependencies/
Installer/dependencies/downloads/dotnet-sdk-9.0.203-win-x64.exe
Installer/dependencies/downloads/VC_redist.x64.exe
```

### Download and Install Inno Setup

1. Download and install Inno Setup from [here](https://jrsoftware.org/isdl.php).
2. The TiXL solution includes an `Installer/` folder containing `installer.iss`.
3. Open `installer.iss` in Inno Setup (e.g., by double-clicking).
4. Click the blue "Play" button to start the build process and wait a few minutes.
5. The installer will run for testing.
6. The output artifact will be created at `Installer/Output/`.
