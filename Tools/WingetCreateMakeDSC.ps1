# This script is a prototype for quickly creating DSC files.

#Powershell 7 Required
if ($(Get-Host).version.major -lt 7) {
  Write-Host 'This script requires powershell 7. You can update powershell by typing winget install Microsoft.Powershell.' -ForegroundColor red
  Exit(1)
}

# Create a custom exception type for our dependency management
class UnmetDependencyException : Exception {
  UnmetDependencyException([string] $message) : base($message) {}
  UnmetDependencyException([string] $message, [Exception] $exception) : base($message, $exception) {}
}

# Ensure the Winget PowerShell modules are installed
if (-not(Get-Module -ListAvailable -Name Microsoft.Winget.Client)) {
  try {
    Install-Module Microsoft.Winget.Client
  } catch {
    # If there was an exception while installing, pass it as an InternalException for further debugging
    throw [UnmetDependencyException]::new("'Microsoft.Winget.Client' was not installed successfully", $_.Exception)
  } finally {
    # Check to be sure it acutally installed
    if (-not(Get-Module -ListAvailable -Name Microsoft.Winget.Client)) {
      throw [UnmetDependencyException]::new("`Microsoft.Winget.Client` was not found. Check that you have installed the Windows Package Manager modules correctly.")
    }
  }
}

# Ensure the powershell-yaml module is installed
if (-not(Get-Module -ListAvailable -Name powershell-yaml)) {
  try {
    Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force
    Install-Module -Name powershell-yaml -Force -Repository PSGallery -Scope CurrentUser
  } catch {
    # If there was an exception while installing, pass it as an InternalException for further debugging
    throw [UnmetDependencyException]::new("'powershell-yaml' was not installed successfully", $_.Exception)
  } finally {
    # Check to be sure it acutally installed
    if (-not(Get-Module -ListAvailable -Name powershell-yaml)) {
      throw [UnmetDependencyException]::new("`powershell-yaml` was not found. Check that you have installed the module correctly.")
    }
  }
}

[System.Collections.ArrayList]$finalPackages = @()
$configurationVersion = '0.2.0'
$Utf8NoBomEncoding = New-Object System.Text.UTF8Encoding $False
$DSCHeader = "# yaml-language-server: `$schema=https://aka.ms/configuration-dsc-schema/$($configurationVersion -Replace '\.0$','')"

do {
  $findResult = Find-WinGetPackage $(Read-Host 'What is the Winget ID, or name of the package you want to add to the configuration file?')
  
  if ($findResult.count -ne 0) {
    # Assign an index to each package
    $findResult | ForEach-Object { $script:i = 1 } { Add-Member -InputObject $_ -NotePropertyName Index -NotePropertyValue $i; $i++ }
    $findResult | Select-Object -Property Index, Name, Id, Version | Format-Table | Out-Host

    $packageSelected = $false
    while (-not($packageSelected)) {
      Write-Host
      # Prompt user for selection string
      $selection = (Read-Host 'Select a package by Index, Name, or Id. Press enter to continue or skip')
      $selectedPackage = $null
      # If user didn't enter any selection string, set no package as selected and continue
      if ( [string]::IsNullOrWhiteSpace($selection) ) {
        $packageSelected = $true
      } elseif ( $selection -in $findResult.Id ) {
        # If the user entered a string which matches the Id, select that package
        $selectedPackage = $findResult.Where({ $_.Id -eq $selection })
        $packageSelected = $true
      } elseif ( $selection -in $findResult.Name ) {
        # If the user entered a string which matches the Name, select that package
        # Because names could conflict, take the first item in the list to avoid error
        $selectedPackage = $findResult.Where({ $_.Name -eq $selection }) | Select-Object -First 1
        $packageSelected = $true
      } else {
        # If the name and ID didn't match, try selecting by index
        # This needs to be a try-catch to handle converting strings to integers
        try {
          $selectedPackage = $findResult.Where({ $_.Index -eq [int]$selection })
          # If the user selects an index out of range, don't set no package as selected. Instead, allow for correcting the index
          # If the intent is to select no package, the user will be able to skip after being notified the index is out of range
          if ($selectedPackage) {
            $packageSelected = $true
          } else {
            Write-Host 'Index out of range, please try again'
          }
        } catch {
          Write-Host 'Invalid entry, please try again'
        }
      }
    }

    # If a package was selected, add it to the package list; Otherwise, continue
    if ($selectedPackage) {
      $unit = @{'resource' = 'Microsoft.WinGet.DSC/WinGetPackage'; 'directives' = @{'description' = $selectedPackage.Name; 'allowPrerelease' = $true; }; 'settings' = @{'id' = $selectedPackage.Id; 'source' = $selectedPackage.Source } }
      [void]$finalPackages.Add($unit)
      Write-Host "Added $($selectedPackage.Name)" -ForegroundColor Blue
    }
  
  } else {
    Write-Host 'No package found matching input criteria.' -ForegroundColor DarkYellow
  }
}  while ($(Read-Host 'Would you like to add another package? [y/n]') -eq 'y')

Write-Host
$fileName = Read-Host 'Name of the configuration file (without extension)'
$filePath = Join-Path -Path (Get-Location) -ChildPath "$($fileName).winget"

$rawYaml = ConvertTo-Yaml @{'properties' = @{'resources' = $finalPackages; 'configurationVersion' = $configurationVersion } }
[System.IO.File]::WriteAllLines($filePath, @($DSCHeader, '', $rawYaml.trim()), $Utf8NoBomEncoding)

Write-Host
Write-Host 'Testing resulting file...' -ForegroundColor yellow
(&winget configure --help) > $null

if ($LASTEXITCODE -eq 0) {
  winget configure validate --file $filePath
} else {
  Write-Host "'winget configure' is not available, skipping validation." -ForegroundColor Yellow
}

Write-Host "Configuration file created at: $($filePath)" -ForegroundColor Green