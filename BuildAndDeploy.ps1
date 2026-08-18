# ============================================================
#  RogueForge + MyAwesomeMod 一键构建并部署脚本
#  使用方式：
#    1) 双击运行（完成后按回车关闭）
#    2) VS Code 中 Ctrl+Shift+B（任务）或右键菜单 "RogueForge: 一键构建并部署"
# ============================================================
param([switch]$NoPause)

$ErrorActionPreference = 'Stop'

# ---------- 可修改的路径配置 ----------
$DotNet   = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path $DotNet)) { $DotNet = 'dotnet' }

$SlnRF    = 'd:\C#项目\地痞街区mod开发\美化城市\MyAwesomeMod.sln'
$PrjMain  = 'd:\C#项目\地痞街区mod开发\丰富的城市系统\MyAwesomeMod\Mod.csproj'

$OutRF    = 'd:\C#项目\地痞街区mod开发\美化城市\MyAwesomeMod\bin\Debug\net471\RogueForge.dll'
$OutMain  = 'd:\C#项目\地痞街区mod开发\丰富的城市系统\MyAwesomeMod\bin\Debug\net471\MyAwesomeMod.dll'

$RefDir   = 'd:\C#项目\地痞街区mod开发\.ref'
$Plugins  = 'E:\Steam\steamapps\common\Streets of Rogue\BepInEx\plugins'

# 附加建筑库目录：把你自己写的建筑 dll（如 TrashCan.dll，纯类库即可，不需要插件入口）
# 放进本目录，部署时会一并复制到游戏 plugins，由 RogueForge 自动扫描加载（多 dll 支持）。
$UserLibs = 'd:\C#项目\地痞街区mod开发\美化城市\UserLibs'
# --------------------------------------

function Write-Step($msg) { Write-Host "`n$msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "      -> $msg" -ForegroundColor Green }
function Write-Err($msg)  { Write-Host "[错误] $msg" -ForegroundColor Red }

Write-Host '================================================'
Write-Host '  一键构建 + 部署'
Write-Host '  1) RogueForge.dll   -> .ref 和游戏插件目录'
Write-Host '  2) MyAwesomeMod.dll -> 游戏插件目录'
Write-Host '  3) UserLibs\*.dll   -> 游戏插件目录（可选，多 dll 支持）'
Write-Host '================================================'

# ---------- Step 1: 构建 RogueForge 库 ----------
Write-Step '[1/2] 构建 RogueForge 库 ...'
& $DotNet build $SlnRF -c Debug --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Err 'RogueForge 构建失败（存在编译错误），已中止，未部署任何 DLL！'
    if (-not $NoPause) { Read-Host '按回车键关闭窗口' }
    exit 1
}
if (-not (Test-Path $OutRF)) {
    Write-Err 'RogueForge.dll 未生成，构建失败！'
    if (-not $NoPause) { Read-Host '按回车键关闭窗口' }
    exit 1
}
Write-Step '[1/2] 复制 RogueForge.dll -> .ref 和游戏插件目录 ...'
Copy-Item $OutRF "$RefDir\RogueForge.dll" -Force
Copy-Item $OutRF "$Plugins\RogueForge.dll" -Force
Write-Ok 'RogueForge.dll 部署完成'

# ---------- Step 2: 构建 MyAwesomeMod ----------
Write-Step '[2/2] 构建 MyAwesomeMod 主模组 ...'
# 注意：PostBuild 的 PluginBuildEvents.exe 已加 IgnoreExitCode，不会再阻塞构建；
# 这里检查 MSBuild 退出码，只有编译真失败才中止（避免复制旧 DLL）。
& $DotNet build $PrjMain -c Debug --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Err 'MyAwesomeMod 构建失败（存在编译错误），已中止，未部署任何 DLL！'
    if (-not $NoPause) { Read-Host '按回车键关闭窗口' }
    exit 1
}
if (-not (Test-Path $OutMain)) {
    Write-Err 'MyAwesomeMod.dll 未生成，构建失败！'
    if (-not $NoPause) { Read-Host '按回车键关闭窗口' }
    exit 1
}
Write-Step '[2/2] 复制 MyAwesomeMod.dll -> 游戏插件目录 ...'
Copy-Item $OutMain "$Plugins\MyAwesomeMod.dll" -Force
Write-Ok 'RogueForge.dll && MyAwesomeMod.dll 部署完成'

# ---------- Step 3（可选）: 附加建筑库（UserLibs\*.dll） ----------
Write-Step '[3/3] 复制 UserLibs 附加建筑库 -> 游戏插件目录（可选）...'
if (Test-Path $UserLibs) {
    $libs = Get-ChildItem "$UserLibs" -Filter '*.dll' -File
    if ($libs.Count -gt 0) {
        foreach ($lib in $libs) {
            Copy-Item $lib.FullName "$Plugins\$($lib.Name)" -Force
            Write-Ok "已复制 $($lib.Name) -> plugins（RogueForge 会自动扫描加载）"
        }
    }
    else {
        Write-Ok "UserLibs 目录存在但没有 dll，跳过"
    }
}
else {
    Write-Ok "UserLibs 目录不存在（$UserLibs），跳过"
}

Write-Host ''
Write-Host '================================================' -ForegroundColor Green
Write-Host '  全部完成！DLL 已部署到：' -ForegroundColor Green
Write-Host "    $Plugins" -ForegroundColor Green
Write-Host '================================================' -ForegroundColor Green

if (-not $NoPause) { Read-Host '按回车键关闭窗口' }
