try {
    $r = [System.Net.HttpWebRequest]::Create('https://localhost:5002/api/datahub/negotiate')
    $r.Method = 'POST'
    $r.ContentType = 'application/json'
    $r.Accept = 'application/json'
    $resp = $r.GetResponse()
    $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
    $body = $reader.ReadToEnd()
    Write-Host "STATUS: $([int]$resp.StatusCode)"
    Write-Host "BODY: $body"
} catch {
    Write-Host "ERROR: $_"
}
