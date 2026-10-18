param(
    [Parameter(Mandatory=$true)][string]$ApkPath,
    [Parameter(Mandatory=$true)][string]$ArtifactDirectory,
    [string]$AdbPath = 'C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe'
)
$ErrorActionPreference = 'Stop'
trap { Write-Output $_.ScriptStackTrace; break }
$testPackage = 'com.companyname.fintracker.sharedcheck'
$testDevice = 'emulator-5554'
$testPassword = [Guid]::NewGuid().ToString('N')
$testUsername = 'navcheck' + [DateTime]::UtcNow.ToString('MMddHHmmss')

# Only the separately built emulator test package is installed. Never clear user app data.
function Adb { & $AdbPath -s $testDevice @args }
function Dump {
    for ($attempt = 0; $attempt -lt 4; $attempt++) {
        Adb shell uiautomator dump /sdcard/fintracker-shared-check.xml 2>$null | Out-Null
        $xmlText = (Adb shell cat /sdcard/fintracker-shared-check.xml 2>$null) -join "`n"
        if ($xmlText.StartsWith('<?xml')) { return [xml]$xmlText }
        Start-Sleep -Seconds 1
    }
    throw 'The emulator accessibility tree is not ready.'
}
function TapNode($node) {
    if ($null -eq $node) { throw 'Required UI element not found.' }
    $bounds = [regex]::Matches($node.bounds, '\d+') | ForEach-Object { [int]$_.Value }
    Adb shell input tap ([int](($bounds[0] + $bounds[2]) / 2)) ([int](($bounds[1] + $bounds[3]) / 2)) | Out-Null
    Start-Sleep -Milliseconds 500
}
function TapText([string]$text) {
    $tree = Dump
    $nodes = @($tree.SelectNodes('//node') | Where-Object { $_.text -eq $text })
    $node = $nodes | Where-Object { $_.clickable -eq 'true' } | Select-Object -First 1
    if ($null -eq $node) { $node = $nodes | Select-Object -First 1 }
    TapNode $node
}
function TapDescription([string]$description) {
    $tree = Dump
    TapNode ($tree.SelectNodes('//node') | Where-Object { $_.'content-desc' -eq $description } | Select-Object -First 1)
}
function AssertText([string]$text) {
    $tree = Dump
    if (-not ($tree.SelectNodes('//node') | Where-Object { $_.text -eq $text })) { throw "Missing expected text: $text" }
    Write-Output "PASS: $text"
}
function AssertDescription([string]$description) {
    $tree = Dump
    if (-not ($tree.SelectNodes('//node') | Where-Object { $_.'content-desc' -eq $description })) { throw "Missing expected action: $description" }
    Write-Output "PASS: $description"
}
function Capture([string]$name) {
    Adb shell screencap -p /sdcard/fintracker-shared-check.png | Out-Null
    Adb pull /sdcard/fintracker-shared-check.png (Join-Path $ArtifactDirectory ($name + '.png')) | Out-Null
}
function AssertTopBar([string]$name, [string]$expectedColor) {
    Capture $name
    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::new((Join-Path $ArtifactDirectory ($name + '.png')))
    try {
        # Fixed emulator-5554 (1080x2400): sample clear background away from clock/logo/icons.
        foreach ($sample in @(@(500, 70), @(1040, 170))) {
            $pixel = $bitmap.GetPixel($sample[0], $sample[1])
            $actual = '#{0:X2}{1:X2}{2:X2}' -f $pixel.R, $pixel.G, $pixel.B
            if ($actual -ne $expectedColor) { throw "$name top-bar pixel is $actual, expected $expectedColor" }
        }
        $whitePixels = 0
        for ($x = 80; $x -lt 170; $x += 2) {
            for ($y = 40; $y -lt 95; $y += 2) {
                $pixel = $bitmap.GetPixel($x, $y)
                if ($pixel.R -gt 220 -and $pixel.G -gt 220 -and $pixel.B -gt 220) { $whitePixels++ }
            }
        }
        if ($whitePixels -lt 10) { throw "$name status clock is not readable white text" }
        Write-Output "PASS: $name status strip and toolbar match $expectedColor, with white status text"
    } finally { $bitmap.Dispose() }
}
function SelectPicker([string]$current, [string]$choice) {
    if ($current -eq $choice) { return }
    TapText $current
    TapText $choice
}
function HideKeyboard {
    # Android Back can pop a MAUI page on this emulator instead of dismissing IME.
    Adb shell input keyevent 66 | Out-Null
}
function FillFields([string[]]$values) {
    for ($i = 0; $i -lt $values.Length; $i++) {
        $tree = Dump
        $fields = @($tree.SelectNodes('//node') | Where-Object { $_.class -eq 'android.widget.EditText' })
        TapNode $fields[$i]
        Adb shell input text $values[$i] | Out-Null
        HideKeyboard
    }
}

if (-not (Test-Path -LiteralPath $ArtifactDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $ArtifactDirectory | Out-Null
}
if ([IO.Path]::GetFileName($ApkPath) -ne ($testPackage + '-Signed.apk')) { throw 'Use the isolated sharedcheck APK, not the normal application APK.' }
Adb install --no-incremental -r $ApkPath
if ($LASTEXITCODE -ne 0) { throw 'APK installation failed.' }
Adb shell monkey -p $testPackage -c android.intent.category.LAUNCHER 1 | Out-Null
Start-Sleep -Seconds 10
Capture '01-welcome'
$tree = Dump
$continue = $tree.SelectNodes('//node') | Where-Object { $_.'content-desc' -eq 'Continue to login' } | Select-Object -First 1
if ($continue) { TapNode $continue; Start-Sleep -Seconds 1 }
AssertText 'Welcome Back'
TapText 'Register'
FillFields @('Navigation%sCheck', $testUsername, $testPassword, $testPassword)
TapText 'Register'
Start-Sleep -Seconds 1
TapText 'OK'
AssertText 'Welcome Back'
FillFields @($testUsername, $testPassword)
TapText 'Login'
Start-Sleep -Seconds 2
AssertText 'Home'
AssertText 'Reports'
AssertText 'Calendar'
Capture '02-home'
TapDescription 'search'
HideKeyboard
Write-Output 'PASS: Header search action invoked'
TapText 'Reports'
AssertText 'Expense report'
TapText 'Expense report'
AssertText 'Expense Report'
AssertText 'No expenses found for this period.'
AssertDescription 'User profile and account actions'
Capture '03-report'
TapText 'Home'
TapDescription 'Add income or expense'
TapText 'Income'
AssertText 'Income'
$tree = Dump
if ($tree.SelectNodes('//node') | Where-Object { $_.'content-desc' -eq 'Add income or expense' }) { throw 'Bottom navigation must be hidden on entry forms.' }
Capture '04-income-entry'
Adb shell input keyevent 4 | Out-Null
TapText 'Calendar'
Start-Sleep -Seconds 1
AssertText 'Daily Expense Trend'
Capture '05-calendar'
TapText 'More'
Adb shell input swipe 400 1900 400 1100 300 | Out-Null
AssertText 'Show introduction'
TapText 'Setup'
AssertText 'Setup'
Capture '06-setup'
$selectedColor = 'Red'
$selectedAppearance = ((Dump).SelectNodes('//node') | Where-Object { $_.class -eq 'android.widget.EditText' -and $_.text -in @('System', 'Light', 'Dark') } | Select-Object -First 1).text
foreach ($testTheme in @(
    @{ Name='Blue'; Hex='#1246A4'; Appearance='Light' },
    @{ Name='Green'; Hex='#055B35'; Appearance='Dark' },
    @{ Name='Purple'; Hex='#2B0B98'; Appearance='System' },
    @{ Name='Red'; Hex='#8E1B12'; Appearance='Light' },
    @{ Name='Blue'; Hex='#1246A4'; Appearance='Dark' }
)) {
    SelectPicker $selectedColor $testTheme.Name
    SelectPicker $selectedAppearance $testTheme.Appearance
    TapText 'Apply Theme'
    $selectedColor = $testTheme.Name
    $selectedAppearance = $testTheme.Appearance
    AssertTopBar ('theme-' + $selectedColor + '-' + $selectedAppearance) $testTheme.Hex
    if ($selectedAppearance -eq 'System') {
        Adb shell am force-stop $testPackage | Out-Null
        Adb shell monkey -p $testPackage -c android.intent.category.LAUNCHER 1 | Out-Null
        Start-Sleep -Seconds 4
        AssertText 'Home'
        AssertTopBar 'theme-system-restored' $testTheme.Hex
        TapText 'More'
        TapText 'Setup'
        AssertText 'System'
    }
}
TapText 'Home'
AssertTopBar 'theme-home' '#1246A4'
TapText 'Reports'
TapText 'Expense report'
AssertText 'Expense Report'
AssertText 'No expenses found for this period.'
Start-Sleep -Seconds 1
AssertTopBar 'theme-report' '#1246A4'
Adb shell input keyevent 3 | Out-Null
Adb shell monkey -p $testPackage -c android.intent.category.LAUNCHER 1 | Out-Null
Start-Sleep -Seconds 1
AssertTopBar 'theme-resumed' '#1246A4'
Adb shell am force-stop $testPackage | Out-Null
Adb shell monkey -p $testPackage -c android.intent.category.LAUNCHER 1 | Out-Null
Start-Sleep -Seconds 4
AssertText 'Home'
AssertTopBar 'theme-restored' '#1246A4'
TapText 'More'
TapText 'Setup'
AssertText 'Blue'
AssertText 'Dark'
TapDescription 'User profile and account actions'
TapText 'Logout'
AssertText 'Welcome Back'
Capture '07-logged-out'
AssertTopBar 'theme-logged-out' '#1246A4'
$tree = Dump
if ($tree.SelectNodes('//node') | Where-Object { $_.'content-desc' -eq 'Add income or expense' }) { throw 'Authenticated bottom navigation leaked onto login.' }
Adb shell am force-stop $testPackage | Out-Null
Adb shell monkey -p $testPackage -c android.intent.category.LAUNCHER 1 | Out-Null
Start-Sleep -Seconds 2
AssertText 'Welcome Back'
AssertTopBar 'theme-logged-out-restart' '#1246A4'
Write-Output 'PASS: Returning logged-out startup skips onboarding'
Write-Output 'Smoke test completed. No financial entries were saved. Test credentials were generated in memory only.'
