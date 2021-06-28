
# Winget-Create CLI Settings

You can configure Winget-Create by editing the `settings.json` file. Running `wingetcreate settings` will open the file in the default json editor; if no editor is configured, Windows will prompt for you to select an editor, and Notepad is sensible option if you have no other preference.

## Telemetry

The `telemetry` settings control whether Winget-Create writes ETW events that may be sent to Microsoft on a default installation of Windows.

See [details on telemetry](https://github.com/microsoft/winget-create#datatelemetry), and our [primary privacy statement](../PRIVACY.md).

### disable

```json
    "telemetry": {
        "disable": true
    },
```

If set to true, the `telemetry.disable` setting will prevent any event from being written by the program.