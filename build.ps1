$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '找不到系统 .NET Framework 编译器。' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
if ($sources.Count -lt 2) { throw '程序源文件不完整。' }
$output = Join-Path $PSScriptRoot 'To do 清单.exe'
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',
    '/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll',
    '/main:CrayonTodo.Program',('/out:' + $output))
$icon = Join-Path $PSScriptRoot 'assets\app.ico'
if (Test-Path -LiteralPath $icon) { $arguments += ('/win32icon:' + $icon) }
$sticker = Join-Path $PSScriptRoot 'assets\tomato-sticker.png'
if (Test-Path -LiteralPath $sticker) { $arguments += ('/resource:' + $sticker + ',CrayonTodo.TomatoSticker') }
$shinchan = Join-Path $PSScriptRoot 'assets\shinchan.png'
if (-not (Test-Path -LiteralPath $shinchan)) { throw '找不到蜡笔小新插画。' }
$arguments += ('/resource:' + $shinchan + ',CrayonTodo.Shinchan')
$avatar = Join-Path $PSScriptRoot 'assets\shinchan-avatar.png'
if (-not (Test-Path -LiteralPath $avatar)) { throw '找不到小新悬浮头像。' }
$arguments += ('/resource:' + $avatar + ',CrayonTodo.ShinchanAvatar')
$themeAssets = @{
    'kitty.png' = 'CrayonTodo.Kitty'
    'kuromi.png' = 'CrayonTodo.Kuromi'
    'doraemon.png' = 'CrayonTodo.Doraemon'
    'spongebob-transparent.png' = 'CrayonTodo.SpongeBob'
}
foreach ($entry in $themeAssets.GetEnumerator()) {
    $path = Join-Path $PSScriptRoot ('assets\' + $entry.Key)
    if (-not (Test-Path -LiteralPath $path)) { throw ('找不到主题素材：' + $entry.Key) }
    $arguments += ('/resource:' + $path + ',' + $entry.Value)
}
& $compiler @arguments $sources
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
Write-Output ('已生成：' + $output)
