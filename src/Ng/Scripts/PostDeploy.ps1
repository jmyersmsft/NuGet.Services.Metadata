$serviceNameC2R = $OctopusParameters["Jobs.Catalog2Registration.Service.Name"]
$serviceNameC2L = $OctopusParameters["Jobs.Catalog2Lucene.Service.Name"]

$currentDirectory = [string](Get-Location)

# Delete services if they exist
if (Get-Service $serviceNameC2R -ErrorAction SilentlyContinue)
{
    sc.exe delete $serviceNameC2R 
}
if (Get-Service $serviceNameC2L -ErrorAction SilentlyContinue)
{
    sc.exe delete $serviceNameC2L 
}

# Install services
# Uses nssm - from http://nssm.cc/download
$installC2R = "nssm install $serviceNameC2R $currentDirectory\Catalog2RegistrationV3.cmd"
cmd /C $installC2R 
Set-Service -Name $serviceNameC2R -DisplayName "NuGet - Catalog2RegistrationV3 - $serviceNameC2R" -Description "Runs Catalog2RegistrationV3." -StartupType Automatic
sc.exe failure $serviceNameC2R reset= 30 actions= restart/5000 

$installC2L = "nssm install $serviceNameC2L $currentDirectory\Catalog2LuceneV3.cmd"
cmd /C $installC2L 
Set-Service -Name $serviceNameC2L -DisplayName "NuGet - Catalog2LuceneV3 - $serviceNameC2L" -Description "Runs Catalog2LuceneV3." -StartupType Automatic
sc.exe failure $serviceNameC2L reset= 30 actions= restart/5000 

# Run services
net start $serviceNameC2R
net start $serviceNameC2L
