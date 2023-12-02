# This script is a prototype for quickly creating DSC files.

#Powershell 7 Required
if ($(host).version.major -lt 7) {
  Write-host "This script requires powershell 7. You can update powershell by typing winget install Microsoft.Powershell." -ForegroundColor red
  Exit(1)
}

#Set output encoding to UTF-8
$OutputEncoding = [ System.Text.Encoding]::UTF8   

if ($null -eq (Get-InstalledModule -Name Microsoft.Winget.Client -ErrorAction 'SilentlyContinue'))
{
  try {  Install-Module Microsoft.Winget.Client
  } catch {
  #Pass the exception 
  throw [System.Net.WebException]::new("Error retrieving powershell module: Microsoft.Winget.Client. Check that you have installed the Windows Package Manager modules correctly.", $_.Exception)
  #bugbug is this good enough?
  }
}

if ($null -eq (Get-InstalledModule -Name powershell-yaml -ErrorAction 'SilentlyContinue'))
{
  try {
    Install-Module powershell-yaml
  } catch {
  #Pass the exception 
  throw [System.Net.WebException]::new("Error retrieving powershell module: powershell-yaml. Check that you have installed the Windows Package Manager modules correctly.", $_.Exception)
  #bugbug is this good enough?
  }
}

[System.Collections.ArrayList]$finalPackages = @()
$configurationVersion = "0.2.0" 

do
{
  $appId = Read-Host "What is the Winget ID, or name of the package you want to add to the configuration file?"
  $findResult = Find-WinGetPackage $appId
  
  if ($findResult.count -ne 0)
  {
    # Assign an index to each package
    $findResult | ForEach-Object { $i=1 } { Add-Member -InputObject $_ -NotePropertyName Index -NotePropertyValue $i; $i++ }
    $findResult | Select-Object -Property Index,Name,Id,Version | Out-Host

    $selection = -1
    $packageSelected = $false
    while (-not($packageSelected))
    {
      write-host
      # TODO: We should capture against bad value. "string"
      # TODO: We should allow user to skip.  Maybe hit S or X.
      $selection = [int](Read-Host "Input the number of the package you want to select")
      if (($selection -gt $findResult.count) -or ($selection -lt 1))
      {
        Write-Host "Selection is out of range, try again."
      }
      else
      {
        $packageSelected = $true 
      }
    }

    $selectedPackage = $findResult[$selection - 1] 

    #Specify Source 
    #Winget currently has 2 sources. If the ID contains a period, we will assume winget.
    #otherwise it is the MSSTORE.  We are not accounting for private REPOs at this time.
    If ($selectedPackage.Id -like "*.*") {
      $source="winget"
    } else {
      $source="msstore"
    }
 
    $unit = @{"resource" = "Microsoft.WinGet.DSC/WinGetPackage"; "directives" = @{"description" = $selectedPackage.Name; "allowPrerelease" = $true; }; "settings" = @{"id" = $selectedPackage.Id; "source"=$source }}
    $tempvar = $finalPackages.Add($unit)
    write-host Added  $selectedPackage.Name -ForegroundColor blue
 
  
  }
  else
  {
    Write-Host "No package found matching input criteria." -ForegroundColor DarkYellow
  }
}  while ($(Read-Host "Would you like to add another package? [y/n]") -eq 'y')

Write-host
$fileName = Read-Host "Name of the configuration file (without extension)"
$filePath = Join-Path -Path (Get-Location) -ChildPath "$($fileName).winget"

ConvertTo-Yaml @{"properties"= @{"resources"=$finalPackages; "configurationVersion"= $configurationVersion}} -OutFile $filePath -Force

Write-Host
Write-Host Testing resulting file.  -ForegroundColor yellow
(&winget configure --help) > $null

if ($LASTEXITCODE -eq 0) {
  winget configure validate --file $filePath
}
else {
  Write-Host "'winget configure' is not available, skipping validation." -ForegroundColor Yellow
}

Write-Host "Configuration file created at: $($filePath)" -ForegroundColor Green