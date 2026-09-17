# Earthdata Auth Setup for .NET

This folder contains a C# / .NET version of the Earthdata authentication setup utility.

## Files

- `Program.cs` - the console application source code
- `EarthdataAuthSetupDotNet.csproj` - the .NET project file

## What it does

The program creates these files in your target folder:

- `.urs_cookies`
- `.dodsrc`
- `.netrc`
- `.edl_token`

It can also:

- prompt for your NASA Earthdata username and password
- open the Earthdata login page in your browser
- create an Earthdata token from your login credentials

## Requirements

To build and run this project, install the **.NET 8 SDK** or newer.

If you only have the .NET runtime installed, the program source is present but the `dotnet build` and `dotnet run` commands will not work until the SDK is installed.

## Run from PowerShell

If the SDK is installed, use:

```powershell
dotnet run --project "C:\Users\JDoe\earthdata_auth_setup_dotnet\EarthdataAuthSetupDotNet.csproj" -- --target-dir "C:\Users\JDoe\NLDASV2_Giovanni_BASINS"
```

## Open the Earthdata login page first

```powershell
dotnet run --project "C:\Users\JDoe\earthdata_auth_setup_dotnet\EarthdataAuthSetupDotNet.csproj" -- --open-login-page --target-dir "C:\Users\JDoe\NLDASV2_Giovanni_BASINS"
```

## Optional command-line arguments

- `--target-dir <path>`: folder where the auth files should be created
- `--username <value>`: Earthdata username
- `--password <value>`: Earthdata password
- `--force-rewrite-netrc`: always overwrite the local `.netrc`
- `--open-login-page`: open the Earthdata login page in your browser
- `--mock-token <value>`: skip the live token request and write a fake token instead
- `--help`: show help

## Build only

```powershell
dotnet build "C:\Users\JDoe\earthdata_auth_setup_dotnet\EarthdataAuthSetupDotNet.csproj"
```

## Publish an executable

```powershell
dotnet publish "C:\Users\JDoe\earthdata_auth_setup_dotnet\EarthdataAuthSetupDotNet.csproj" -c Release -r win-x64 --self-contained false
```

## Safe local test without contacting Earthdata

This writes a fake token so you can confirm the file creation flow:

```powershell
dotnet run --project "C:\Users\JDoe\earthdata_auth_setup_dotnet\EarthdataAuthSetupDotNet.csproj" -- --username demo_user --password demo_pass --mock-token fake-token-123 --target-dir "C:\Users\JDoe\NLDASV2_Giovanni_BASINS\test_auth_output"
```

