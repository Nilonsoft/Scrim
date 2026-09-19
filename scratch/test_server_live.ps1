Add-Type -Path "C:\Users\donwo\source\Scrim\src\bin\Debug\net10.0-windows10.0.19041.0\Scrim.dll"

$profile = New-Object Scrim.Configuration.ScrimProfile
$profile.Port = 19444
$profile.EnableNetworkAccess = $true

$profileManager = [Moq.Mock[Scrim.Configuration.IProfileManager]]::new()
[void]$profileManager.Setup([System.Linq.Expressions.Expression]::Lambda([Func[Scrim.Configuration.ScrimProfile]]{ $profile })).Returns($profile)

$meta = [Moq.Mock[Scrim.Metadata.IMetadataService]]::new()
$hub = New-Object Scrim.Server.BroadcastHub
$req = New-Object Scrim.Metadata.SongRequestController
$net = New-Object Scrim.Server.NetworkDiscoveryService
$theme = New-Object Scrim.Configuration.ThemeService
$chat = New-Object Scrim.Server.LiveChatService
$reaction = New-Object Scrim.Metadata.SongReactionService
$history = New-Object Scrim.Metadata.SongHistoryService

$server = New-Object Scrim.Server.HttpStreamServer(
    $hub, $meta.Object, $req, $profileManager.Object, $net, $theme, $chat, $reaction, $history
)

try {
    Write-Host "Starting server on port 19444 with EnableNetworkAccess = true..."
    $server.Start(19444)
    Start-Sleep -Milliseconds 500

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(5)

    Write-Host "Fetching /api/status..."
    $resp = $client.GetAsync("http://localhost:19444/api/status").Result
    Write-Host "Status response code: $($resp.StatusCode)"
    $content = $resp.Content.ReadAsStringAsync().Result
    Write-Host "Status response body: $content"

    Write-Host "Fetching / (index.html)..."
    $resp2 = $client.GetAsync("http://localhost:19444/").Result
    Write-Host "Index response code: $($resp2.StatusCode)"

    Write-Host "Testing SSE connection /api/events..."
    $sseResp = $client.GetAsync("http://localhost:19444/api/events", [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).Result
    Write-Host "SSE response code: $($sseResp.StatusCode)"
    $stream = $sseResp.Content.ReadAsStreamAsync().Result
    $reader = [System.IO.StreamReader]::new($stream)
    
    $readTask = $reader.ReadLineAsync()
    if ($readTask.Wait(3000)) {
        Write-Host "SSE First Line: $($readTask.Result)"
    } else {
        Write-Host "TIMEOUT reading SSE first line! Bridge is hanging/buffering!"
    }

} catch {
    Write-Host "ERROR: $_"
} finally {
    $server.Stop()
    Write-Host "Done."
}
