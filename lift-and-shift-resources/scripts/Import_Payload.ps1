# Script to Create Import body for ADHS FHIR service

Param(
    # $export Content-Location URI
    [Parameter(Mandatory = $true, HelpMessage="Content-Location URI")]
    $url
)

$file_count = 10000

$cl_url = [uri]$url
$host_name = $cl_url.Host

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

    # Get the Auth token
    Write-Host "Fetching the Auth Token to call the `$export` Content-Location URL." -ForegroundColor Yellow
    
    $Access_token = az account get-access-token --scope "https://$host_name/.default"
    $token = $Access_token[1].TrimEnd(",","`"").Split(":")[1].TrimStart(" ","`"")
    
    Write-Host "Fetching of Auth Token completed." -ForegroundColor Green

    # Call the $export Content-Location URL to get the output.
    Write-Host "Calling the Content-Location URL to get the `$export` output " -ForegroundColor Yellow
    $output = Invoke-RestMethod -Headers @{Authorization = "Bearer $token"} -Uri $url -Method GET -ContentType 'application/json'
    Write-Host "Output fetching completed." -ForegroundColor Green

    # Create Import Body Json
    Write-Host "Creating the Import body payload." -ForegroundColor Yellow
    if ($output.output.Count -le $file_count) {
        
        $jsonBase = @{}
        [System.Collections.ArrayList]$parameter= @()
        [System.Collections.ArrayList]$input_part= @()

        $parameter.Add(@{"name"="inputFormat";"valueString"="application/fhir+ndjson"}) > $null
        $parameter.Add(@{"name"="mode";"valueString"="IncrementalLoad"}) > $null

        foreach ($i in $output.output){
            if($i.type -ne "SearchParameter"){
                $type = $i.type
                $url = $i.url
                $input_part.Add(@{"name"="type";"valueString"=$type}) > $null
                $input_part.Add(@{"name"="url";"valueString"=$url}) > $null
                $parameter.Add(@{"name"="input";"part"=$input_part}) > $null
                [System.Collections.ArrayList]$input_part= @()
            }
        }

        $jsonBase.Add("parameter",$parameter) > $null
        $jsonBase.Add("resourceType","Parameters") > $null

        $Folder = '.\Import_Payload'
        if (-not (Test-Path $Folder)) {
            New-Item -Path '.\Import_Payload' -ItemType Directory > $null
        }  
        $jsonBase | ConvertTo-Json -Depth 5 | Out-File ".\Import_Payload\import_payload.json"
        Write-Host "Creation of Import body is completed." -ForegroundColor Green
    }

    <# Maximum number of files to be imported per operation is 10,000. 
    So if files are more than 10,000 below condition will execute for creating multiple
    import body payload #>
    
    if ($output.output.Count -ge $file_count) {
        $counter = 0
        $import_payload_count = 1

        $jsonBase = @{}
        [System.Collections.ArrayList]$parameter= @()
        [System.Collections.ArrayList]$input_part= @()
        $parameter.Add(@{"name"="inputFormat";"valueString"="application/fhir+ndjson"}) > $null
        $parameter.Add(@{"name"="mode";"valueString"="IncrementalLoad"}) > $null

        for ($i = 0; $i -le $output.output.Count; $i++) {

            if($output.output[$i].type -ne "SearchParameter"){
            
            if($counter -eq $file_count){

                $jsonBase.Add("parameter",$parameter) > $null
                $jsonBase.Add("resourceType","Parameters") > $null

                $Folder = '.\Import_Payload'
                if (-not (Test-Path $Folder)) {
                    New-Item -Path '.\Import_Payload' -ItemType Directory > $null
                }  
                $jsonBase | ConvertTo-Json -Depth 5 | Out-File ".\Import_Payload\import_payload_$import_payload_count.json"
                Write-Host "Creation of Import body is completed." -ForegroundColor Green
                
                $jsonBase = @{}
                [System.Collections.ArrayList]$parameter= @()
                [System.Collections.ArrayList]$input_part= @()
                $parameter.Add(@{"name"="inputFormat";"valueString"="application/fhir+ndjson"}) > $null
                $parameter.Add(@{"name"="mode";"valueString"="IncrementalLoad"}) > $null

                $counter = 0
                $import_payload_count++
            }

            elseif ($output.output.Count -eq $i -and $parameter.Count -ge 2 ) {
                $jsonBase.Add("parameter",$parameter) > $null
                $jsonBase.Add("resourceType","Parameters") > $null

                $Folder = '.\Import_Payload'
                if (-not (Test-Path $Folder)) {
                    New-Item -Path '.\Import_Payload' -ItemType Directory > $null
                }  
                $jsonBase | ConvertTo-Json -Depth 5 | Out-File ".\Import_Payload\import_payload_$import_payload_count.json"
                Write-Host "Creation of Import body is completed." -ForegroundColor Green
            }

            if ($output.output[$i]) {
                $type = $output.output[$i].type
                $url = $output.output[$i].url
                $input_part.Add(@{"name"="type";"valueString"=$type}) > $null
                $input_part.Add(@{"name"="url";"valueString"=$url}) > $null

                $parameter.Add(@{"name"="input";"part"=$input_part}) > $null

                [System.Collections.ArrayList]$input_part= @()
                $counter++
            }                                                              
        }   
    }                     
}
catch {
    Write-Host "An error occurred:"
    Write-Host $_
}
