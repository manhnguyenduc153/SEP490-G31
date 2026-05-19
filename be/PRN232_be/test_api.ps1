$baseUrl = "http://localhost:49548"
$sellerEmail = "rahoget355@daerdy.com"
$sellerPassword = "Tiennguyen13a11"

Write-Host "Logging in..."
$loginBody = @{
    email    = $sellerEmail
    password = $sellerPassword
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $token = $loginResponse.accessToken
    Write-Host "Login successful. Token acquired."

    $headers = @{
        Authorization = "Bearer $token"
    }

    Write-Host "`nFetching Notifications..."
    $notifResponse = Invoke-RestMethod -Uri "$baseUrl/api/notification" -Method Get -Headers $headers
    Write-Host "Raw Response:"
    $notifResponse | ConvertTo-Json -Depth 5
}
catch {
    Write-Host "Error: $_"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $reader.BaseStream.Position = 0
        $errBody = $reader.ReadToEnd()
        Write-Host "Error Body: $errBody"
    }
}
