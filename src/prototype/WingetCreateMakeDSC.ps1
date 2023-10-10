# This script is a prototype for quickly creating DSC files.
# 

param($inputfile)
# Bugbug this only seems to run in powershell 7, or the winget modules fail. It would be good to detect that.

Try
{
   find-wingetpackage Microsoft.VisualStudioCode
} 
catch 
{
# Missing the powershell module
  Write-host Downloading winget module
  # Bugbug make the path dynamic
  $source = 'https://github.com/microsoft/winget-cli/releases/download/v1.5.441-preview/Microsoft.WinGet.Client-PSModule.zip'
  
  $destination = '.\Microsoft.WinGet.Client-PSModule.zip'

  if (-not(test-path $destination  )) {
  Invoke-RestMethod -Uri $source -OutFile $destination
  }
  $wingetmodule=".\Modules\Microsoft.WinGet.Client.psd1"
  Write-host Extracting winget module to 
    if (-not(test-path $wingetmodule  )) {
      Expand-Archive -Path $destination -DestinationPath ".\modules"
    } 
  
  Import-Module .\Modules\Microsoft.WinGet.Client.psd1
  pause

}

 

$complete="n"
$filename = read-host "Enter the path and filename to the DSC file you want to edit or create"
while ($complete -eq "n") {
  $badapp="false"
  if (-not(test-Path ($filename))) {out-file $filename} else {write-host Updating file $filename}
  $oldfile = get-content $filename

  $TempAppName = Read-host "Which app would you like to search for?"

#Search  for package
  $wingetsearch=find-wingetpackage  $TempAppName

# Bugbug this method does not always work and may have an issue with the count.

  if ($wingetsearch.count -eq 1) {
    write-host Number of Apps: $wingetsearch.count -foregroundcolor green
    write-host Found $wingetsearch.name     $wingetsearch.ID -foregroundcolor blue
	$yn= Read-Host "Is that the you want?  [y/n]"

 

	if($yn -eq "n") {
		$badapp="true"
	}
	else {
	  $appname=$wingetsearch.ID
	}
  
  } else {
    $i=0
    Foreach ($row in  $wingetsearch){
        $i++
        $string =  "[" + $i + "]" +"|"+   $row.name +"|"+    $row.id
        write-host $string
    }

	write-host "Looks like you have too many choices. Choose the correct ID and we will start over." -foregroundcolor red
    $number=read-host "Or type in the number of the item"

    If ($number -gt 0 -and $number -lt $wingetsearch.count) {
        $appname=$wingetsearch[$number-1].id
    } else {
        $badapp="true"
    }
  }

  if ($badapp -eq "false" ){
 
#Define Variables
    $resource = "    - resource: Microsoft.WinGet.DSC/WinGetPackage"
    
    $id = "      id: " + $appname
    $directives= "      directives:"
    $description = "        description: " + $wingetsearch.name
    $allowPrerelease = "        allowPrerelease: true"
    $settings= "      settings:"
    $settingsid ="        id: " + $appname
    $source ="        source: winget"
    $suffix= "  configurationVersion: 0.2.0"
    Set-Content -Path $filename -Value "properties:"
    add-Content -Path $filename -Value "  resources:"
    if ($oldfile.length -gt 1 ){
    #need to restore content but not add properties and suffix
      foreach ($line in $oldfile) {
          if ($line -ne "properties:" -and (-not($line -like "*configurationVersion*")) -and (-not($line -like "*resources:*"))){
              add-Content -Path $filename -Value $line  
          }
      }
    }

    add-Content -Path $filename -Value $resource 
    add-Content -Path $filename -Value $id 
    add-Content -Path $filename -Value $directives 
    add-Content -Path $filename -Value $description 
    add-Content -Path $filename -Value $allowPrerelease 
    add-Content -Path $filename -Value $settings 
    add-Content -Path $filename -Value $settingsid  
    add-Content -Path $filename -Value $source 
    add-Content -Path $filename -Value $suffix  

    type $filename
    $complete = Read-host "All done? y/n"  
    }
}

Write-host Congratulations! You created $filename -foregroundcolor blue