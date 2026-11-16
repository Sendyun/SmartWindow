param(
    [ValidateSet("toggle", "previous", "next", "seek")]
    [string]$Action = "toggle",
    [string]$AppIdMatch = "",
    [double]$PositionSeconds = 0,
    [switch]$Server,
    [int]$ParentPid = 0
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
Add-Type -AssemblyName System.Runtime.WindowsRuntime

$transportType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType = WindowsRuntime]
$script:mediaManager = $null
$asTaskMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() |
    Where-Object {
        $_.Name -eq "AsTask" -and
        $_.IsGenericMethod -and
        $_.GetParameters().Count -eq 1 -and
        $_.GetParameters()[0].ParameterType.Name -like "IAsyncOperation*"
    } |
    Select-Object -First 1

function Await-WinRt($operation, [Type]$resultType) {
    $boundMethod = $asTaskMethod.MakeGenericMethod($resultType)
    $task = $boundMethod.Invoke($null, @($operation))
    $task.Wait()
    return $task.Result
}

function Invoke-MediaAction(
    [string]$RequestedAction,
    [string]$RequestedAppId,
    [double]$RequestedPositionSeconds
) {
    if ($null -eq $script:mediaManager) {
        $script:mediaManager = Await-WinRt `
            ($transportType::RequestAsync()) `
            $transportType
    }
    $sessions = @($script:mediaManager.GetSessions())

    $session = $null
    if (-not [string]::IsNullOrWhiteSpace($RequestedAppId)) {
        $session = $sessions |
            Where-Object {
                $_.SourceAppUserModelId -eq $RequestedAppId
            } |
            Select-Object -First 1
    }
    if ($null -eq $session) {
        $session = $sessions |
            Where-Object {
                $_.GetPlaybackInfo().PlaybackStatus.ToString() -eq "Playing"
            } |
            Select-Object -First 1
    }
    if ($null -eq $session) {
        $session = $sessions | Select-Object -First 1
    }
    if ($null -eq $session) {
        return $false
    }

    if ($RequestedAction -eq "seek") {
        $positionTicks = [long](
            [math]::Max(0, $RequestedPositionSeconds) * 10000000
        )
        $operation = $session.TryChangePlaybackPositionAsync(
            $positionTicks)
    }
    elseif ($RequestedAction -eq "previous") {
        $operation = $session.TrySkipPreviousAsync()
    }
    elseif ($RequestedAction -eq "next") {
        $operation = $session.TrySkipNextAsync()
    }
    else {
        $operation = $session.TryTogglePlayPauseAsync()
    }
    return [bool](Await-WinRt $operation ([bool]))
}

if (-not $Server) {
    $result = Invoke-MediaAction $Action $AppIdMatch $PositionSeconds
    [Console]::Out.WriteLine($result)
    exit
}

$pipeName = "SmartWindow.MediaCommands.$ParentPid"
$pipe = New-Object System.IO.Pipes.NamedPipeServerStream(
    $pipeName,
    [System.IO.Pipes.PipeDirection]::InOut,
    1,
    [System.IO.Pipes.PipeTransmissionMode]::Byte,
    [System.IO.Pipes.PipeOptions]::Asynchronous
)

try {
    $running = $true
    while ($running) {
        try {
            Get-Process -Id $ParentPid -ErrorAction Stop | Out-Null
        }
        catch {
            break
        }

        $waitTask = $pipe.WaitForConnectionAsync()
        while (-not $waitTask.Wait(2000)) {
            try {
                Get-Process -Id $ParentPid -ErrorAction Stop | Out-Null
            }
            catch {
                $running = $false
                break
            }
        }
        if (-not $running -or -not $pipe.IsConnected) {
            continue
        }

        $reader = New-Object System.IO.StreamReader(
            $pipe,
            [System.Text.Encoding]::UTF8,
            $false,
            1024,
            $true
        )
        $writer = New-Object System.IO.StreamWriter(
            $pipe,
            (New-Object System.Text.UTF8Encoding($false)),
            1024,
            $true
        )
        try {
            while ($pipe.IsConnected) {
                $line = $reader.ReadLine()
                if ($null -eq $line) {
                    break
                }
                try {
                    $parts = $line.Split([char]9, 2)
                    $requestedAction = $parts[0]
                    $requestedPositionSeconds = 0.0
                    if ($requestedAction.StartsWith("seek:")) {
                        [void][double]::TryParse(
                            $requestedAction.Substring(5),
                            [System.Globalization.NumberStyles]::Float,
                            [System.Globalization.CultureInfo]::InvariantCulture,
                            [ref]$requestedPositionSeconds)
                        $requestedAction = "seek"
                    }
                    $requestedAppId = ""
                    if ($parts.Length -gt 1 -and
                        -not [string]::IsNullOrWhiteSpace($parts[1])) {
                        $requestedAppId = [System.Text.Encoding]::UTF8.GetString(
                            [Convert]::FromBase64String($parts[1])
                        )
                    }
                    $result = Invoke-MediaAction `
                        $requestedAction `
                        $requestedAppId `
                        $requestedPositionSeconds
                }
                catch {
                    [Console]::Error.WriteLine($_.Exception.Message)
                    $result = $false
                }
                $writer.WriteLine($(if ($result) { "True" } else { "False" }))
                $writer.Flush()
            }
        }
        finally {
            try {
                $reader.Dispose()
            }
            catch {
            }
            try {
                $writer.Dispose()
            }
            catch {
            }
            try {
                if ($pipe.IsConnected) {
                    $pipe.Disconnect()
                }
            }
            catch {
            }
        }
    }
}
finally {
    $pipe.Dispose()
}
