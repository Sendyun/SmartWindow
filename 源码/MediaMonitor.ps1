param(
    [int]$ParentPid = 0
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

Add-Type -AssemblyName System.Runtime.WindowsRuntime

$transportType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType = WindowsRuntime]
$propertiesType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties, Windows.Media.Control, ContentType = WindowsRuntime]
$streamInterfaceType = [Windows.Storage.Streams.IRandomAccessStreamWithContentType, Windows.Storage.Streams, ContentType = WindowsRuntime]

$asTaskMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() |
    Where-Object {
        $_.Name -eq "AsTask" -and
        $_.IsGenericMethod -and
        $_.GetParameters().Count -eq 1 -and
        $_.GetParameters()[0].ParameterType.Name -like "IAsyncOperation*"
    } |
    Select-Object -First 1

$asStreamMethod = [System.IO.WindowsRuntimeStreamExtensions].GetMethods() |
    Where-Object {
        $_.Name -eq "AsStreamForRead" -and
        $_.GetParameters().Count -eq 1
    } |
    Select-Object -First 1

$artDirectory = Join-Path $env:LOCALAPPDATA "SmartWindow"
$artPath = Join-Path $artDirectory "media-art.img"
[System.IO.Directory]::CreateDirectory($artDirectory) | Out-Null

function Await-WinRt($operation, [Type]$resultType) {
    $boundMethod = $asTaskMethod.MakeGenericMethod($resultType)
    $task = $boundMethod.Invoke($null, @($operation))
    $task.Wait()
    return $task.Result
}

function Encode-Text([object]$value) {
    if ($null -eq $value) {
        return ""
    }
    return [Convert]::ToBase64String(
        [System.Text.Encoding]::UTF8.GetBytes([string]$value)
    )
}

function Save-Thumbnail($thumbnail, [string]$destination) {
    $stream = Await-WinRt ($thumbnail.OpenReadAsync()) $streamInterfaceType
    $netStream = $asStreamMethod.Invoke($null, @($stream))
    $content = New-Object System.IO.MemoryStream
    $netStream.CopyTo($content)
    $bytes = $content.ToArray()
    $temporary = $destination + ".tmp"
    [System.IO.File]::WriteAllBytes($temporary, $bytes)
    if ([System.IO.File]::Exists($destination)) {
        [System.IO.File]::Delete($destination)
    }
    [System.IO.File]::Move($temporary, $destination)
}

$manager = Await-WinRt ($transportType::RequestAsync()) $transportType
$lastLine = ""
$lastArtworkSignature = ""
$parentCheckCounter = 0

while ($true) {
    if ($ParentPid -gt 0) {
        $parentCheckCounter++
        if (($parentCheckCounter % 8) -eq 0) {
            try {
                Get-Process -Id $ParentPid -ErrorAction Stop | Out-Null
            }
            catch {
                break
            }
        }
    }
    try {
        $playingSession = $null
        foreach ($session in $manager.GetSessions()) {
            if ($session.GetPlaybackInfo().PlaybackStatus.ToString() -eq "Playing") {
                $playingSession = $session
                break
            }
        }

        if ($null -eq $playingSession) {
            if ($lastLine -ne "SW1`t0") {
                $lastLine = "SW1`t0"
                [Console]::Out.WriteLine($lastLine)
                [Console]::Out.Flush()
            }
            Start-Sleep -Milliseconds 320
            continue
        }

        $playback = $playingSession.GetPlaybackInfo()
        $timeline = $playingSession.GetTimelineProperties()
        $properties = Await-WinRt ($playingSession.TryGetMediaPropertiesAsync()) $propertiesType
        $appId = [string]$playingSession.SourceAppUserModelId
        $title = [string]$properties.Title
        $artist = [string]$properties.Artist
        $album = [string]$properties.AlbumTitle
        $signature = $appId + "`n" + $title + "`n" + $artist + "`n" + $album

        if ($signature -ne $lastArtworkSignature) {
            $artworkSaved = $false
            if ($null -ne $properties.Thumbnail) {
                try {
                    Save-Thumbnail $properties.Thumbnail $artPath
                    $artworkSaved = $true
                }
                catch {
                    if ([System.IO.File]::Exists($artPath)) {
                        [System.IO.File]::Delete($artPath)
                    }
                }
            }
            elseif ([System.IO.File]::Exists($artPath)) {
                [System.IO.File]::Delete($artPath)
            }
            # The media session may publish its thumbnail a few polls after
            # the title changes. Keep retrying until the image was saved.
            if ($artworkSaved) {
                $lastArtworkSignature = $signature
            }
        }

        $availableArtPath = ""
        if ([System.IO.File]::Exists($artPath)) {
            $availableArtPath = $artPath
        }
        $positionSeconds = [math]::Round(
            $timeline.Position.TotalSeconds,
            0)
        $durationSeconds = [math]::Max(
            0,
            [math]::Round(
                ($timeline.EndTime - $timeline.StartTime).TotalSeconds,
                0))
        $invariant = [System.Globalization.CultureInfo]::InvariantCulture

        $line = "SW1`t1`t" +
            (Encode-Text $appId) + "`t" +
            (Encode-Text $title) + "`t" +
            (Encode-Text $artist) + "`t" +
            (Encode-Text $album) + "`t" +
            (Encode-Text $signature) + "`t" +
            (Encode-Text $availableArtPath) + "`t" +
            $positionSeconds.ToString($invariant) + "`t" +
            $durationSeconds.ToString($invariant)

        if ($line -ne $lastLine) {
            $lastLine = $line
            [Console]::Out.WriteLine($line)
            [Console]::Out.Flush()
        }
    }
    catch {
        [Console]::Error.WriteLine($_.Exception.Message)
    }
    Start-Sleep -Milliseconds 200
}
