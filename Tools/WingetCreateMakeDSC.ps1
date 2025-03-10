param ( $verbose )
# This script is a prototype for quickly creating DSC files.

function workloads (){
  $list=@("Microsoft.VisualStudio.Workload.VisualStudioExtension","Microsoft.VisualStudio.Workload.CoreEditor",
  "Microsoft.VisualStudio.Workload.Azure",  "Microsoft.VisualStudio.Workload.Data",   "Microsoft.VisualStudio.Workload.DataScience",
  "Microsoft.VisualStudio.Workload.ManagedDesktop",   "Microsoft.VisualStudio.Workload.ManagedGame",   "Microsoft.VisualStudio.Workload.NativeCrossPlat",
  "Microsoft.VisualStudio.Workload.NativeDesktop",   "Microsoft.VisualStudio.Workload.NativeGame",   "Microsoft.VisualStudio.Workload.NativeMobile",
  "Microsoft.VisualStudio.Workload.NetCrossPlat",
  "Microsoft.VisualStudio.Workload.NetWeb",
  "Microsoft.VisualStudio.Workload.Node",
  "Microsoft.VisualStudio.Workload.Office",
  "Microsoft.VisualStudio.Workload.Python",
  "Microsoft.VisualStudio.Workload.Universal",
  "Microsoft.VisualStudio.Workload.VisualStudioExtension")

  $Index=1
  $returnvalue=@()
  foreach ($workload in $list)
  {
      $packageDetails = "[$($Index)] $($workload)  "
      Write-Host $packageDetails

      $Index++
  }
    $selection=$null
   
    Write-host Type in workload or workloads wanted by entering the index number or numbers.
    write-host   "If you want all, simply type all."
    while (-not($selection))
    {
      $selection = [string](Read-Host "Selection")
      
    }
    if ($selection -eq "all")
    {
        return $selection
    } else 
    {
      $listoptions=@($selection.split(" "))
   
      foreach ($value in $listoptions){
          
           $vcounter=1
          foreach($entry in $list )
          {
              if ($value -eq $vcounter )
              {
                $returnvalue+=($entry)
              }
              $vcounter++
          }
          
      }
      return $returnvalue
    }
    

}

function writeworkloads($name, $dependson)
{

  $unit = @{"resource" = "Microsoft.VisualStudio.DSC/VSComponents"; 
   ;"id" = $name;  "directives" = @{"description" = "Install required VS Workloads (Universal)"; 
   "allowPrerelease" = $true; }; "settings" = @{"productId" = $name; 
   "channelId" =  "VisualStudio.17.Release"; "components" = @($name)};"dependsOn" = @($dependson)}
 
   return $unit
}




#Powershell 7 Required


if (($verbose -eq "-v") -or ($verbose -eq "--verbose")) { $verbose = "true"}


$hostdata=host
write-host Powershell Version: $hostdata.version.major
if ($hostdata.version.major -lt 7) {
  Write-host "This script requires powershell 7. You can update powershell by typing winget install Microsoft.Powershell." -ForegroundColor red
  [Environment]::Exit(1) 
}

#Set output encoding to UTF-8
$OutputEncoding = [ System.Text.Encoding]::UTF8   

if ($null -eq (Get-InstalledModule -Name Microsoft.Winget.Client))
{
  try {  Install-Module Microsoft.Winget.Client
  } catch {
  #Pass the exception 
  throw [System.Net.WebException]::new("Error retrieving powershell module: Microsoft.Winget.Client. Check that you have installed the Windows Package Manager modules correctly.", $_.Exception)
  #bugbug is this good enough?
  }
}

if ($null -eq (Get-InstalledModule -Name powershell-yaml))
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

$continue = $true
do
{
  $appId = Read-Host "What is the Winget ID, or name of the package you want to add to the configuration file?"
  $findResult = Find-WinGetPackage $appId
  
  if ($findResult.count -ne 0)
  {
    $Index=1
    foreach ($package in $findResult)
    {
        $packageDetails = "[$($Index)] $($package.Name) | $($package.Id) | $($package.Version)"
        Write-Host $packageDetails
        $index++
    }

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
 
    $unit = @{"resource" = "Microsoft.WinGet.DSC/WinGetPackage"; "id" = $selectedPackage.Id; "directives" = @{"description" = $selectedPackage.Name; "allowPrerelease" = $true; }; "settings" = @{"id" = $selectedPackage.Id; "source"=$source }}
    $tempvar = $finalPackages.Add($unit)
    write-host Added  $selectedPackage.Name -ForegroundColor blue
    if ($verbose -eq "true"){
  	$yaml=ConvertTo-Yaml @{"properties"= @{"resources"=$unit; "configurationVersion"= $configurationVersion}} 
	  Write-Host $yaml
    }
     
    if ($selectedPackage.Name -like "*visual studio*")
    {
      $yn=Read-Host "Noticed that you installed Visual Studio. Would you like to add workloads? [y/n]"
      if ($yn -eq "y") 
      {
        $workloads = workloads 


        foreach ($wk in $workloads)
          {
            write-host Adding workload $wk -ForegroundColor blue 
            
            $ret=writeworkloads $wk   $selectedPackage.Id;

            $tempvar = $finalPackages.Add($ret)
            if ($verbose -eq "true"){
              $yaml=ConvertTo-Yaml @{"properties"= @{"resources"=$ret; "configurationVersion"= $configurationVersion}} 

            }

          }
      }
    }
  
  }
  else
  {
    Write-Host "No package found matching input criteria." -ForegroundColor DarkYellow
  }
}  while ($(Read-Host "Would you like to add another package? [y/n]") -eq 'y')

Write-Host
 
if((Read-Host "Would you like to turn on DevMode? [y/n]") -eq 'y')
{

    $unit = @{"resource" = "Microsoft.Windows.Developer/DeveloperMode"; "id" = "Enable"; "directives" = @{"description" = "Enable Developer Mode"; "allowPrerelease" = $true; }; "settings" = @{"Ensure" = "Present" }}
    $tempvar = $finalPackages.Add($unit)

    write-host DevMode added -ForegroundColor blue
}

if((Read-Host "Would you like to add environment variables? [y/n]") -eq 'y')
{
  $c=0
  do{
    $c++

    $var = (Read-Host ("Enter the variable name"))
     
    $newvar = 'Expand-Archive -LiteralPath $env:' + $var

    $idName="Environment"+ $c
    $unit = @{"resource" = "xPSDesiredStateConfiguration/xScript"; "id" = $idName; "directives" = @{"description" = "Sets Environement variables"; "allowPrerelease" = $true; }; "settings" = @{"SetScript" = $newvar }}
    $tempvar = $finalPackages.Add($unit)
 
    write-host Variable $var added -ForegroundColor blue
    
  }  while ($(Read-Host "Would you like to add environment variables? [y/n]") -eq 'y')

}



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