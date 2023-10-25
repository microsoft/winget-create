# This script is a prototype for quickly creating DSC files.

if ($null -eq (Get-InstalledModule -Name Microsoft.Winget.Client))
{
  Install-Module Microsoft.Winget.Client
}

if ($null -eq (Get-InstalledModule -Name powershell-yaml))
{
  Install-Module powershell-yaml
}

[System.Collections.ArrayList]$finalPackages = @()
$configurationVersion = "0.2.0" 

$continue = $true
while ($continue)
{
  $appId = Read-Host "What is the id of your app?"
  $findResult = Find-WinGetPackage $appId
  
  if ($findResult.count -ne 0)
  {
    $index=0
    foreach ($package in $findResult)
    {
        $packageDetails = "[$($index)] $($package.Name) | $($package.Id) | $($package.Version)"
        Write-Host $packageDetails
        $index++
    }

    $selection = -1
    $packageSelected = $false
    while (-not($packageSelected))
    {
      $selection = [int](Read-Host "Input the number of the package you want to select")
      if (($selection -gt $findResult.count) -or ($selection -lt 0))
      {
        Write-Host "Selection is out of range, try again."
      }
      else
      {
        $packageSelected = $true 
      }
    }

    $selectedPackage = $findResult[$selection]

    $unit = @{"resource" = "Microsoft.WinGet.DSC/WinGetPackage"; "directives" = @{"description" = $selectedPackage.Name; "allowPrerelease" = $true; }; "settings" = @{"id" = $selectedPackage.Id; "source"="winget" }}
    $finalPackages.Add($unit)

    $continue = (Read-Host "Would you like to add another package? [y/n]") -eq 'y'
  }
  else
  {
    Write-Host "No package found matching input criteria." -ForegroundColor DarkYellow
  }
}

$fileName = Read-Host "Name of the configuration file (without extension)"
$filePath = Join-Path -Path (Get-Location) -ChildPath "$($fileName).yaml"
ConvertTo-Yaml @{"properties"= @{"resources"=$finalPackages; "configurationVersion"= $configurationVersion}} -OutFile $filePath -Force
Write-Host "Configuration file created at: $($filePath)" -ForegroundColor Green