# dsc command (Winget-Create)

The **dsc** command is the entry point for using DSC (Desired State Coonfiguration) with Winget-Create.

## Usage

`wingetcreate.exe dsc <resource> [<options>]`

## Resources
| Resource | Type | Description | --get | --set | --test | --export | --schema | Link |
| ---- | ------ | ------------| ----- | ----- | ----- | ----- | ----- | ----- |
| `settings` | `Microsoft.WinGetCreate/Settings` | Manage the settings for Winget-Create | ✅ | ✅ | ✅ | ✅ | ✅ | [Details](dsc/settings.md) |

## Arguments

The following arguments are available:

| <div style="width:100px">Argument</div> | Description |
| --------------------------------------- | ------------|
| **-g, --get** | Get the resource state
| **-s, --set** | Set the resource state |
| **-t, --test** | Test the resource state |
| **e, --export** | Get all state instances |
| **--schema** |  Execute the Schema command |
| **-?, --help** |  Gets additional help on this command. |
