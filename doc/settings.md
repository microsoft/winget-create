
# Winget-Create CLI Settings

You can configure Winget-Create by editing the `settings.json` file. Running `wingetcreate.exe settings` will open the file in the default json editor; if no editor is configured, Windows will prompt for you to select an editor, and Notepad is sensible option if you have no other preference.

## Telemetry

The `Telemetry` settings control whether Winget-Create writes ETW events that may be sent to Microsoft on a default installation of Windows.

See [details on telemetry](https://github.com/microsoft/winget-create#datatelemetry), and our [primary privacy statement](../PRIVACY.md).

### disable

```json
    "telemetry": {
        "disable": true
    },
```

If set to true, the `telemetry.disable` setting will prevent any event from being written by the program.

## WindowsPackageManagerRepository

The `WindowsPackageManagerRepository` setting specifies which repository Winget-Create targets. By default, this setting targets the main [`microsoft/winget-pkgs`](https://github.com/microsoft/winget-pkgs) repository but can be changed to target a forked copy of the main repository like a [test](https://github.com/microsoft/winget-pkgs-submission-test) or private production repository. 

### Owner
The `owner` setting specifies who owns the targeted GitHub repository. By default, this is set to `microsoft`.


### Name
The `name` setting specifies the name of the targeted GitHub repository. By default, this is set to `winget-pkgs`.

```json
  "WindowsPackageManagerRepository": {
    "owner": "microsoft",
    "name": "winget-pkgs"
  }
```

