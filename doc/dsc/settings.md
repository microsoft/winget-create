# Settings resource
Manage the settings for Winget-Create

## 📄 Get
```shell
PS C:\> wingetcreate dsc settings --get
{"settings":{"$schema":"https://aka.ms/wingetcreate-settings.schema.0.1.json","Telemetry":{"disable":true},"CleanUp":{"intervalInDays":7,"disable":false},"WindowsPackageManagerRepository":{"owner":"microsoft","name":"winget-pkgs"},"Manifest":{"format":"yaml"},"Visual":{"anonymizePaths":true}}}
```

## 🖨️ Export
ℹ️ Settings resource Get and Export operation output states are identical.
```shell
PS C:\> wingetcreate dsc settings --export
{"settings":{"$schema":"https://aka.ms/wingetcreate-settings.schema.0.1.json","Telemetry":{"disable":true},"CleanUp":{"intervalInDays":7,"disable":false},"WindowsPackageManagerRepository":{"owner":"microsoft","name":"winget-pkgs"},"Manifest":{"format":"yaml"},"Visual":{"anonymizePaths":true}}}
```

## 📝 Set
- Action `Full`: When action is set to Full, the specified settings will be update accordingly and the remaining settings will be set to their default values.
- Action `Partial`: When action is set to Partial, only the specified settings will be updated, and the remaining settings will remain unchanged.
### 🌕 Full
```shell
PS C:\> wingetcreate dsc settings --set '{"settings": { "Telemetry": { "disable": false }}, "action": "Full"}'
{"settings":{"$schema":"https://aka.ms/wingetcreate-settings.schema.0.1.json","Telemetry":{"disable":false},"CleanUp":{"intervalInDays":7,"disable":false},"WindowsPackageManagerRepository":{"owner":"microsoft","name":"winget-pkgs"},"Manifest":{"format":"yaml"},"Visual":{"anonymizePaths":true}},"action":"Full"}
["settings"]
```

### 🌗 Partial
```shell
PS C:\> wingetcreate dsc settings --set '{"settings": { "Telemetry": { "disable": true }}, "action": "Partial"}'
{"settings":{"$schema":"https://aka.ms/wingetcreate-settings.schema.0.1.json","Telemetry":{"disable":true},"CleanUp":{"intervalInDays":7,"disable":false},"WindowsPackageManagerRepository":{"owner":"microsoft","name":"winget-pkgs"},"Manifest":{"format":"yaml"},"Visual":{"anonymizePaths":true}},"action":"Partial"}
["settings"]
```

## 🧪 Test
- Action `Full`: When action is set to Full, the specified settings will be tested accordingly, and the remaining settings will be tested against their default values.
- Action `Partial`: When action is set to Partial, only the specified settings will be tested, and the remaining settings will be omitted from the test.
### 🌕 Full
```shell
PS C:\> wingetcreate dsc settings --test '{"settings": { "Telemetry": { "disable": false }}, "action": "Full"}'
{"settings":{"$schema":"https://aka.ms/wingetcreate-settings.schema.0.1.json","Telemetry":{"disable":true},"CleanUp":{"intervalInDays":7,"disable":false},"WindowsPackageManagerRepository":{"owner":"microsoft","name":"winget-pkgs"},"Manifest":{"format":"yaml"},"Visual":{"anonymizePaths":true}},"action":"Full","_inDesiredState":false}
["settings"]
```

### 🌗 Partial
```shell
PS C:\> wingetcreate dsc settings --test '{"settings": { "Telemetry": { "disable": false }}, "action": "Partial"}'
{"settings":{"$schema":"https://aka.ms/wingetcreate-settings.schema.0.1.json","Telemetry":{"disable":true},"CleanUp":{"intervalInDays":7,"disable":false},"WindowsPackageManagerRepository":{"owner":"microsoft","name":"winget-pkgs"},"Manifest":{"format":"yaml"},"Visual":{"anonymizePaths":true}},"action":"Partial","_inDesiredState":false}
["settings"]
```