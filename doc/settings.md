
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

## CleanUp

The `CleanUp` settings determine whether Winget-Create will handle the removal of temporary files i.e., installers downloaded and logs generated during the manifest creation process. You can view the location of these files using `wingetcreate --info` command. These settings provide control over the decision to remove files or not and the frequency at which this clean up occurs.

### disable

```json
    "CleanUp": {
        "disable": true
    },
```

If set to true, the `CleanUp.disable` setting will prevent any temporary files from being removed by the program.

### intervalInDays

```json
    "CleanUp": {
        "intervalInDays": 7
    },
```

The `intervalInDays` setting specifies how often Winget-Create will remove temporary files. By default, this is set to 7 days.

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

## Visual

The `Visual` settings control the appearance of the Winget-Create CLI output.

### anonymizePaths

The `anonymizePaths` setting controls whether the paths of files and directories are anonymized in the Winget-Create CLI output. This means that a path such as `C:\Users\user\Documents\manifests\` will be displayed as `%USERPROFILE%\Documents\manifests` (i.e., substitute environment variables where possible). By default, this is set to `true`.

```json
    "Visual": {
        "anonymizePaths": true
    }
```
