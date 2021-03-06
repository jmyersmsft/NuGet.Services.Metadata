$serviceNameC2R = $OctopusParameters["Jobs.Catalog2Registration.Service.Name"]
$serviceNameC2L = $OctopusParameters["Jobs.Catalog2Lucene.Service.Name"]
$serviceNameC2D = $OctopusParameters["Jobs.Catalog2Dnx.Service.Name"]

# Stop and remove services

Write-Host Removing services...
if (Get-Service $serviceNameC2R -ErrorAction SilentlyContinue)
{
    Stop-Service $serviceNameC2R -Force
    sc.exe delete $serviceNameC2R 
}
if (Get-Service $serviceNameC2L -ErrorAction SilentlyContinue)
{
    Stop-Service $serviceNameC2L -Force
    sc.exe delete $serviceNameC2L 
}
if (Get-Service $serviceNameC2D -ErrorAction SilentlyContinue)
{
    Stop-Service $serviceNameC2D -Force
    sc.exe delete $serviceNameC2D 
}
Write-Host Removed services.