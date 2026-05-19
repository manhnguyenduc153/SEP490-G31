try {
    # Test with missing email to trigger model validation error
    $body = @{
        # email is missing
        password        = "Password123!"
        confirmPassword = "Password123!"
        businessName    = "Test Business"
    } | ConvertTo-Json

    $response = Invoke-WebRequest -Uri "http://localhost:49548/api/auth/register" -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
}
catch {
    $response = $_.Exception.Response
    Write-Host "Status Code: " $response.StatusCode.Value__
    Write-Host "Content-Type: " $response.ContentType
    $stream = $response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $errorBody = $reader.ReadToEnd()
    Write-Host "Error Body: " $errorBody
}
