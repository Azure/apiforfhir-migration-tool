# Script to Create Import body for ADHS FHIR service

Param(
    # export Storage account name
    [Parameter(Mandatory = $true, HelpMessage="Export storage account name")]
    $storageaccount,

    # anonymization demo file path
    [Parameter(Mandatory = $true, HelpMessage="Anonymization demo file path")]
    $filepath
)

try {

    #Check Az Module and user logged in    
    $Azmodule_check = Get-Command az -ErrorVariable Azmodule_check -ErrorAction SilentlyContinue
    if (!$Azmodule_check) {
        Write-Host "Az CLI is not installed. Please install the az cli and re-run the script." -ForegroundColor Red
        Exit
    }

    $User_Check = az account show 2>&1
    if (!$?) {
        Write-Host "User not logged into the az account. Please login using az login." -ForegroundColor Red
        Exit
    }

    $ctx = New-AzStorageContext -StorageAccountName $storageaccount -UseConnectedAccount

    Get-AzStorageContainer -Name anonymization -Context $ctx -ErrorAction SilentlyContinue

    if ($? -eq $false) {
        # do what you need to do when it does not exist.
        New-AzStorageContainer -Name anonymization -Context $ctx
    } else {
        # do what you need to do when it does exist.
        Write-Host "Anonymization container already exist in storage account"
    }

    Set-AzStorageBlobContent -File $filepath -Container anonymization -Blob "DemoConfig.json" -Context $ctx
}
catch {
    Write-Host "An error occurred:"
    Write-Host $_
}
