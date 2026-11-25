#Requires -Version 7
param(
    [Parameter(Mandatory)]
    [uri] $BaseUrl
)

$ErrorActionPreference = 'Stop'
if ($BaseUrl.Scheme -notin @('http', 'https')) {
    throw 'Deployment URL must use HTTP or HTTPS.'
}

# Check the deployed shell, SPA fallback, and static assets without changing backend data.
$checks = @(
    @{ Path = '/'; ContentType = 'text/html'; Pattern = 'id="app"' },
    @{ Path = '/products'; ContentType = 'text/html'; Pattern = 'id="app"' },
    @{ Path = '/css/TheShop.css'; ContentType = 'text/css'; Pattern = '\.shop-' },
    @{ Path = '/_framework/blazor.webassembly.js'; ContentType = 'javascript'; Pattern = 'Blazor' }
)

foreach ($check in $checks) {
    $url = [uri]::new($BaseUrl, $check.Path)
    for ($attempt = 1; $attempt -le 6; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $url -TimeoutSec 20
            if ($response.StatusCode -ne 200 -or
                $response.Headers['Content-Type'] -notmatch $check.ContentType -or
                $response.Content -notmatch $check.Pattern) {
                throw "Unexpected deployment response: $url"
            }
            Write-Host "Verified $url"
            break
        }
        catch {
            if ($attempt -eq 6) { throw }
            Start-Sleep -Seconds 5
        }
    }
}
