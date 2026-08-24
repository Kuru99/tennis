param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$outputDirectory = Join-Path $ProjectRoot 'Assets\_Project\Resources\CardArt'
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

$canvasSize = 1254

function New-Pen([System.Drawing.Color]$color, [float]$width) {
    $pen = [System.Drawing.Pen]::new($color, $width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    return $pen
}

function Draw-Leaf([System.Drawing.Graphics]$graphics, [System.Drawing.PointF]$center, [float]$width,
    [float]$height, [float]$angle, [System.Drawing.Color]$color) {
    $state = $graphics.Save()
    $graphics.TranslateTransform($center.X, $center.Y)
    $graphics.RotateTransform($angle)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddBezier(-$width * 0.5, 0, -$width * 0.18, -$height * 0.5, $width * 0.3, -$height * 0.35, $width * 0.5, 0)
    $path.AddBezier($width * 0.5, 0, $width * 0.18, $height * 0.5, -$width * 0.3, $height * 0.35, -$width * 0.5, 0)
    $path.CloseFigure()
    $brush = [System.Drawing.SolidBrush]::new($color)
    $graphics.FillPath($brush, $path)
    $vein = New-Pen ([System.Drawing.Color]::FromArgb(210, 238, 255, 240)) ([Math]::Max(8, $height * 0.055))
    $graphics.DrawLine($vein, -$width * 0.32, 0, $width * 0.34, 0)
    $vein.Dispose()
    $brush.Dispose()
    $path.Dispose()
    $graphics.Restore($state)
}

function Save-Card([string]$name, [System.Drawing.Color]$topColor, [System.Drawing.Color]$bottomColor,
    [System.Drawing.Color]$accentColor, [scriptblock]$drawSymbol) {
    $bitmap = [System.Drawing.Bitmap]::new($canvasSize, $canvasSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::FromArgb(255, 3, 9, 28))

    $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Rectangle]::new(0, 0, $canvasSize, $canvasSize), $topColor, $bottomColor, 135)
    $graphics.FillRectangle($background, 0, 0, $canvasSize, $canvasSize)
    $background.Dispose()

    $stripeBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(42, $accentColor))
    $stripePath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $stripePath.AddPolygon([System.Drawing.Point[]]@(
        [System.Drawing.Point]::new(-120, 960), [System.Drawing.Point]::new(930, -90),
        [System.Drawing.Point]::new(1240, -90), [System.Drawing.Point]::new(190, 960)))
    $graphics.FillPath($stripeBrush, $stripePath)
    $stripePath.Dispose()
    $stripeBrush.Dispose()

    $courtPen = New-Pen ([System.Drawing.Color]::FromArgb(90, 205, 230, 255)) 10
    $graphics.DrawLine($courtPen, 80, 1050, 1174, 1050)
    $graphics.DrawLine($courtPen, 220, 1180, 627, 840)
    $graphics.DrawLine($courtPen, 1034, 1180, 627, 840)
    $courtPen.Dispose()

    & $drawSymbol $graphics $accentColor

    $borderPen = New-Pen ([System.Drawing.Color]::FromArgb(235, 239, 247, 255)) 18
    $graphics.DrawRectangle($borderPen, 28, 28, $canvasSize - 56, $canvasSize - 56)
    $borderPen.Dispose()
    $accentPen = New-Pen $accentColor 12
    $graphics.DrawLine($accentPen, 72, 74, 390, 74)
    $graphics.DrawLine($accentPen, 864, 1180, 1180, 1180)
    $accentPen.Dispose()

    $path = Join-Path $outputDirectory ($name + '.png')
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

$green = [System.Drawing.Color]::FromArgb(255, 48, 238, 154)
$magenta = [System.Drawing.Color]::FromArgb(255, 255, 54, 154)
$cyan = [System.Drawing.Color]::FromArgb(255, 36, 224, 255)
$crimson = [System.Drawing.Color]::FromArgb(255, 250, 48, 78)
$gold = [System.Drawing.Color]::FromArgb(255, 255, 194, 38)
$paper = [System.Drawing.Color]::FromArgb(255, 242, 250, 255)

Save-Card 'LeafVeil' ([System.Drawing.Color]::FromArgb(255, 8, 64, 55)) ([System.Drawing.Color]::FromArgb(255, 20, 8, 52)) $green {
    param($graphics, $accent)
    Draw-Leaf $graphics ([System.Drawing.PointF]::new(400, 510)) 410 235 -28 $accent
    Draw-Leaf $graphics ([System.Drawing.PointF]::new(745, 435)) 360 205 24 $magenta
    Draw-Leaf $graphics ([System.Drawing.PointF]::new(680, 755)) 440 240 -8 ([System.Drawing.Color]::FromArgb(255, 126, 255, 198))
}

Save-Card 'MischiefCurve' ([System.Drawing.Color]::FromArgb(255, 36, 8, 70)) ([System.Drawing.Color]::FromArgb(255, 4, 54, 61)) $magenta {
    param($graphics, $accent)
    $trail = New-Pen $accent 70
    $graphics.DrawBezier($trail, 180, 840, 360, 180, 920, 1030, 1080, 350)
    $trail.Dispose()
    $inner = New-Pen $paper 20
    $graphics.DrawBezier($inner, 180, 840, 360, 180, 920, 1030, 1080, 350)
    $graphics.DrawEllipse($inner, 950, 220, 190, 190)
    $graphics.DrawArc($inner, 985, 250, 120, 130, -65, 150)
    $inner.Dispose()
}

Save-Card 'TimeTease' ([System.Drawing.Color]::FromArgb(255, 4, 52, 67)) ([System.Drawing.Color]::FromArgb(255, 25, 5, 55)) $cyan {
    param($graphics, $accent)
    $outer = New-Pen $accent 58
    $graphics.DrawEllipse($outer, 270, 230, 714, 714)
    $graphics.DrawArc($outer, 172, 136, 910, 910, 200, 94)
    $graphics.DrawLine($outer, 219, 232, 173, 432)
    $outer.Dispose()
    $hand = New-Pen $paper 34
    $graphics.DrawLine($hand, 627, 587, 627, 345)
    $graphics.DrawLine($hand, 627, 587, 792, 708)
    $graphics.FillEllipse([System.Drawing.SolidBrush]::new($paper), 588, 548, 78, 78)
    $hand.Dispose()
}

Save-Card 'DragonGrace' ([System.Drawing.Color]::FromArgb(255, 75, 8, 20)) ([System.Drawing.Color]::FromArgb(255, 4, 18, 49)) $gold {
    param($graphics, $accent)
    $shield = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $shield.AddPolygon([System.Drawing.Point[]]@(
        [System.Drawing.Point]::new(627, 190), [System.Drawing.Point]::new(930, 310),
        [System.Drawing.Point]::new(860, 760), [System.Drawing.Point]::new(627, 972),
        [System.Drawing.Point]::new(394, 760), [System.Drawing.Point]::new(324, 310)))
    $graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(225, 12, 28, 64)), $shield)
    $outline = New-Pen $accent 42
    $graphics.DrawPath($outline, $shield)
    $graphics.DrawArc($outline, 440, 340, 375, 390, 195, 150)
    $graphics.DrawLine($outline, 627, 342, 760, 588)
    $graphics.DrawLine($outline, 760, 588, 613, 802)
    $outline.Dispose()
    $shield.Dispose()
}

Save-Card 'NobleRetake' ([System.Drawing.Color]::FromArgb(255, 34, 12, 58)) ([System.Drawing.Color]::FromArgb(255, 6, 30, 66)) $paper {
    param($graphics, $accent)
    $rewind = New-Pen $gold 56
    $graphics.DrawArc($rewind, 260, 270, 730, 700, 42, 292)
    $graphics.DrawLine($rewind, 310, 390, 180, 335)
    $graphics.DrawLine($rewind, 180, 335, 218, 482)
    $rewind.Dispose()
    $crown = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $crown.AddPolygon([System.Drawing.Point[]]@(
        [System.Drawing.Point]::new(435, 655), [System.Drawing.Point]::new(470, 470),
        [System.Drawing.Point]::new(610, 590), [System.Drawing.Point]::new(710, 430),
        [System.Drawing.Point]::new(820, 590), [System.Drawing.Point]::new(842, 655)))
    $graphics.FillPath([System.Drawing.SolidBrush]::new($accent), $crown)
    $crown.Dispose()
}

Save-Card 'DragonAwakening' ([System.Drawing.Color]::FromArgb(255, 84, 3, 18)) ([System.Drawing.Color]::FromArgb(255, 3, 11, 34)) $crimson {
    param($graphics, $accent)
    $eye = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $eye.AddBezier(190, 600, 400, 305, 850, 305, 1064, 600)
    $eye.AddBezier(1064, 600, 850, 895, 400, 895, 190, 600)
    $eye.CloseFigure()
    $graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(230, 242, 250, 255)), $eye)
    $outline = New-Pen $accent 48
    $graphics.DrawPath($outline, $eye)
    $graphics.FillEllipse([System.Drawing.SolidBrush]::new($gold), 472, 440, 310, 310)
    $graphics.FillEllipse([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 8, 12, 30)), 596, 448, 62, 294)
    $outline.Dispose()
    $eye.Dispose()
}

Write-Output 'PRIDE_COURT_FANTASY_CARD_ART_GENERATED'
