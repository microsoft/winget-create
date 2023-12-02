# This script is a prototype for quickly creating DSC files.

#Powershell 7 Required
if ($(Get-host).version.major -lt 7) {
  Write-Host 'This script requires powershell 7. You can update powershell by typing winget install Microsoft.Powershell.' -ForegroundColor red
  Exit(1)
}

# Create a custom exception type for our dependency management
class UnmetDependencyException : Exception {
  UnmetDependencyException([string] $message) : base($message) {}
  UnmetDependencyException([string] $message, [Exception] $exception) : base($message, $exception) {}
}

#Set output encoding to UTF-8
$OutputEncoding = [ System.Text.Encoding]::UTF8   

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
      throw [UnmetDependencyException]::new("`powershell-yaml` was not found. Check that you have installed the Windows Package Manager modules correctly.")
    }
  }
}

[System.Collections.ArrayList]$finalPackages = @()
$configurationVersion = '0.2.0'
$Utf8NoBomEncoding = New-Object System.Text.UTF8Encoding $False
$DSCHeader = "# yaml-language-server: `$schema=https://aka.ms/configuration-dsc-schema/$($configurationVersion)"

do {
  $appId = Read-Host 'What is the Winget ID, or name of the package you want to add to the configuration file?'
  $findResult = Find-WinGetPackage $appId
  
  if ($findResult.count -ne 0) {
    # Assign an index to each package
    $findResult | ForEach-Object { $i = 1 } { Add-Member -InputObject $_ -NotePropertyName Index -NotePropertyValue $i; $i++ }
    $findResult | Select-Object -Property Index, Name, Id, Version | Format-Table | Out-Host

    $selection = -1
    $packageSelected = $false
    while (-not($packageSelected)) {
      Write-Host
      # TODO: We should capture against bad value. "string"
      # TODO: We should allow user to skip.  Maybe hit S or X.
      $selection = [int](Read-Host 'Input the number of the package you want to select')
      if ($selection -notin $findResult.Index) {
        Write-Host 'Selection is out of range, try again.'
      } else {
        $packageSelected = $true 
      }
    }

    $selectedPackage = $findResult.Where({ $_.Index -eq $selection }) 
    $unit = @{'resource' = 'Microsoft.WinGet.DSC/WinGetPackage'; 'directives' = @{'description' = $selectedPackage.Name; 'allowPrerelease' = $true; }; 'settings' = @{'id' = $selectedPackage.Id; 'source' = $selectedPackage.Source } }
    [void]$finalPackages.Add($unit)
    Write-Host Added $selectedPackage.Name -ForegroundColor blue
 
  
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
Write-Host Testing resulting file. -ForegroundColor yellow
(&winget configure --help) > $null

if ($LASTEXITCODE -eq 0) {
  winget configure validate --file $filePath
} else {
  Write-Host "'winget configure' is not available, skipping validation." -ForegroundColor Yellow
}

Write-Host "Configuration file created at: $($filePath)" -ForegroundColor Green