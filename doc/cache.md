# cache command (Winget-Create)

The **cache** command of the [Winget-Create](../README.md) tool is designed to help users manage their downloaded installers. Continued usage of the tool can result in many downloaded installers taking up unnecessary space on your machine. This command can help clean up some of the unneeded files.

## Usage

`wingetcreate.exe cache [\<options>]`

## Arguments

The following arguments are available:

| <div style="width:100px">Argument</div> | Description |
| --------------------------------------- | ------------|
| **-c, --clean** | Deletes all downloaded installers in the cache folder |
| **-l, --list** | Lists out all the downloaded installers stored in cache |
| **-o, --open** | Opens the cache folder storing the downloaded installers |
| **-?, --help** |  Gets additional help on this command. |
