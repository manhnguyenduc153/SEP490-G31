try {
    $body = @{
        email = "test_business_err_" + (Get-Date -Format "yyyyMMddHHmmss") + "@gmail.com"
        password = "Password123!"
        confirmPassword = "Password123!"
        businessName = "Test Business"
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "http://localhost:49548/api/auth/register" -Method Post -Body $body -ContentType "application/json"
    $response | ConvertTo-Json
} catch {
    Write-Host "Error Status Code: " $_.Exception.Response.StatusCode.Value__
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    $errorBody = $reader.ReadToEnd()
    Write-Host "Error Body: " $errorBody
}
