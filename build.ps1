$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw '找不到 .NET Framework C# 编译器 csc.exe。'
}
& $compiler /nologo /target:winexe /out:窗口收纳工具.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll WindowPocket.cs
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }
Write-Host '已生成 窗口收纳工具.exe'
