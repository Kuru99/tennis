param(
    [Parameter(Mandatory = $true)]
    [string]$BounceSource,

    [Parameter(Mandatory = $true)]
    [string]$RacketSource,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

function Read-Pcm16MonoWav {
    param([string]$Path)

    $reader = [System.IO.BinaryReader]::new([System.IO.File]::OpenRead($Path))
    try {
        if ([System.Text.Encoding]::ASCII.GetString($reader.ReadBytes(4)) -ne 'RIFF') {
            throw "Unsupported WAV container: $Path"
        }

        [void]$reader.ReadInt32()
        if ([System.Text.Encoding]::ASCII.GetString($reader.ReadBytes(4)) -ne 'WAVE') {
            throw "Unsupported WAV signature: $Path"
        }

        $sampleRate = 0
        $channels = 0
        $bitsPerSample = 0
        $audioFormat = 0
        $data = $null

        while ($reader.BaseStream.Position + 8 -le $reader.BaseStream.Length) {
            $chunkId = [System.Text.Encoding]::ASCII.GetString($reader.ReadBytes(4))
            $chunkSize = $reader.ReadInt32()
            $nextPosition = $reader.BaseStream.Position + $chunkSize + ($chunkSize % 2)

            if ($chunkId -eq 'fmt ') {
                $audioFormat = $reader.ReadInt16()
                $channels = $reader.ReadInt16()
                $sampleRate = $reader.ReadInt32()
                [void]$reader.ReadInt32()
                [void]$reader.ReadInt16()
                $bitsPerSample = $reader.ReadInt16()
            }
            elseif ($chunkId -eq 'data') {
                $data = $reader.ReadBytes($chunkSize)
            }

            $reader.BaseStream.Position = [Math]::Min($nextPosition, $reader.BaseStream.Length)
        }

        if ($audioFormat -ne 1 -or $channels -ne 1 -or $bitsPerSample -ne 16 -or $null -eq $data) {
            throw "Expected PCM16 mono WAV: $Path"
        }

        return [PSCustomObject]@{
            SampleRate = $sampleRate
            Data = $data
        }
    }
    finally {
        $reader.Dispose()
    }
}

function Export-WavSegment {
    param(
        [object]$Source,
        [double]$StartSeconds,
        [double]$DurationSeconds,
        [double]$Gain,
        [string]$OutputPath
    )

    $sampleRate = [int]$Source.SampleRate
    $sourceData = [byte[]]$Source.Data
    $sourceSamples = [int]($sourceData.Length / 2)
    $startSample = [Math]::Max(0, [int][Math]::Round($StartSeconds * $sampleRate))
    $sampleCount = [Math]::Min(
        [int][Math]::Round($DurationSeconds * $sampleRate),
        $sourceSamples - $startSample)
    $fadeInSamples = [Math]::Max(1, [int][Math]::Round(0.003 * $sampleRate))
    $fadeOutSamples = [Math]::Max(1, [int][Math]::Round(0.045 * $sampleRate))
    $outputData = [byte[]]::new($sampleCount * 2)

    for ($i = 0; $i -lt $sampleCount; $i++) {
        $sourceOffset = ($startSample + $i) * 2
        $sample = [System.BitConverter]::ToInt16($sourceData, $sourceOffset)
        $envelope = 1.0
        if ($i -lt $fadeInSamples) {
            $envelope = $i / [double]$fadeInSamples
        }
        elseif ($i -ge $sampleCount - $fadeOutSamples) {
            $envelope = [Math]::Max(0.0, ($sampleCount - 1 - $i) / [double]$fadeOutSamples)
        }

        $scaled = [Math]::Round($sample * $Gain * $envelope)
        $clamped = [int][Math]::Max([int16]::MinValue, [Math]::Min([int16]::MaxValue, $scaled))
        $bytes = [System.BitConverter]::GetBytes([int16]$clamped)
        $outputData[$i * 2] = $bytes[0]
        $outputData[$i * 2 + 1] = $bytes[1]
    }

    $stream = [System.IO.File]::Create($OutputPath)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes('RIFF'))
        $writer.Write(36 + $outputData.Length)
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes('WAVE'))
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes('fmt '))
        $writer.Write(16)
        $writer.Write([int16]1)
        $writer.Write([int16]1)
        $writer.Write($sampleRate)
        $writer.Write($sampleRate * 2)
        $writer.Write([int16]2)
        $writer.Write([int16]16)
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes('data'))
        $writer.Write($outputData.Length)
        $writer.Write($outputData)
    }
    finally {
        $writer.Dispose()
    }
}

$resolvedBounce = (Resolve-Path -LiteralPath $BounceSource).Path
$resolvedRacket = (Resolve-Path -LiteralPath $RacketSource).Path
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$resolvedOutput = (Resolve-Path -LiteralPath $OutputDirectory).Path

$bounce = Read-Pcm16MonoWav -Path $resolvedBounce
$racket = Read-Pcm16MonoWav -Path $resolvedRacket

Export-WavSegment -Source $racket -StartSeconds 0.04 -DurationSeconds 0.34 -Gain 0.78 -OutputPath (Join-Path $resolvedOutput 'RacketHit_Soft.wav')
Export-WavSegment -Source $racket -StartSeconds 2.70 -DurationSeconds 0.36 -Gain 0.92 -OutputPath (Join-Path $resolvedOutput 'RacketHit_Strong.wav')
Export-WavSegment -Source $bounce -StartSeconds 0.00 -DurationSeconds 0.38 -Gain 0.72 -OutputPath (Join-Path $resolvedOutput 'TennisBounce_Soft.wav')
Export-WavSegment -Source $bounce -StartSeconds 3.27 -DurationSeconds 0.42 -Gain 0.88 -OutputPath (Join-Path $resolvedOutput 'TennisBounce_Hard.wav')

Get-ChildItem -LiteralPath $resolvedOutput -Filter '*.wav' | Select-Object Name, Length
