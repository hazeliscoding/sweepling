# Drives a running Sweepling's tray icon and review window through UI Automation, for hand tests.
# Usage: powershell -NoProfile -File scripts/tray.ps1 open <screenshot.png> | done | quit
param([Parameter(Mandatory)] [ValidateSet('open', 'done', 'quit')] [string] $Action, [string] $Screenshot)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type -Namespace W -Name U -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern bool GetCursorPos(out System.Drawing.Point p);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, System.UIntPtr e);
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
'@ -ReferencedAssemblies System.Drawing

$A = [System.Windows.Automation.AutomationElement]; $S = [System.Windows.Automation.TreeScope]; $P = [System.Windows.Automation.PropertyCondition]
$root = $A::RootElement
$sweep = Get-Process Sweepling -ErrorAction Stop | Select-Object -First 1

function Buttons($element) { @($element.FindAll($S::Descendants, (New-Object $P($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))) }
function Overflow {
    $ov = $root.FindFirst($S::Children, (New-Object $P($A::ClassNameProperty, 'TopLevelWindowForOverflowXamlIsland')))
    if (-not $ov -or $ov.Current.BoundingRectangle.Width -eq 0) {
        $tray = $root.FindFirst($S::Children, (New-Object $P($A::ClassNameProperty, 'Shell_TrayWnd')))
        @(Buttons $tray | Where-Object { $_.Current.Name -match 'Hidden Icons' })[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 1000
        $ov = $root.FindFirst($S::Children, (New-Object $P($A::ClassNameProperty, 'TopLevelWindowForOverflowXamlIsland')))
    }
    $ov
}
# The icon is on the taskbar itself once it's pinned there, and in the hidden-icons overflow until then.
function Place {
    $tray = $root.FindFirst($S::Children, (New-Object $P($A::ClassNameProperty, 'Shell_TrayWnd')))
    $buttons = Buttons $tray
    if (-not ($buttons | Where-Object { $_.Current.Name -match '^Sweepling' })) { $buttons = Buttons (Overflow) }
    $buttons
}
function Window { $root.FindFirst($S::Children, (New-Object System.Windows.Automation.AndCondition((New-Object $P($A::ProcessIdProperty, $sweep.Id)), (New-Object $P($A::NameProperty, 'Sweepling'))))) }

switch ($Action) {
    'open' {
        $icon = @(Place | Where-Object { $_.Current.Name -match '^Sweepling' })
        if ($icon.Count -ne 1) { 'STOP: Sweepling icon not found once'; exit 1 }
        $icon[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 2500
        $win = Window
        if (-not $win) { 'STOP: no review window'; exit 1 }
        $r = $win.Current.BoundingRectangle
        'window ' + $r
        if ($Screenshot) {
            $bmp = New-Object System.Drawing.Bitmap ([int]$r.Width), ([int]$r.Height + 40)
            $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size); $bmp.Save($Screenshot); $g.Dispose(); $bmp.Dispose()
        }
    }
    'done' {
        $done = @(Buttons (Window) | Where-Object { $_.Current.Name -eq 'Done' })
        if ($done.Count -ne 1) { 'STOP: no single Done button'; exit 1 }
        $done[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        'pressed Done'
    }
    'quit' {
        # Right-click the icon at a point only it covers, then choose the menu's last item, Quit Sweepling, by
        # posting keys to the menu's own window: UI Automation lists no items in a Win32 tray menu.
        $buttons = @(Place)
        $icon = @($buttons | Where-Object { $_.Current.Name -match '^Sweepling' })
        if ($icon.Count -ne 1) { 'STOP: Sweepling icon not found once'; exit 1 }
        $b = $icon[0].Current.BoundingRectangle; $x = [int]($b.X + $b.Width / 2); $y = [int]($b.Y + $b.Height / 2)
        $hits = @($buttons | Where-Object { $_.Current.BoundingRectangle.Contains((New-Object System.Windows.Point $x, $y)) })
        if ($hits.Count -ne 1 -or $hits[0].Current.Name -notmatch '^Sweepling') { 'STOP: the point is not only on Sweepling'; exit 1 }
        $saved = New-Object System.Drawing.Point; [W.U]::GetCursorPos([ref]$saved) | Out-Null
        [W.U]::SetCursorPos($x, $y) | Out-Null; Start-Sleep -Milliseconds 150
        [W.U]::mouse_event(0x0008, 0, 0, 0, [System.UIntPtr]::Zero); [W.U]::mouse_event(0x0010, 0, 0, 0, [System.UIntPtr]::Zero)
        [W.U]::SetCursorPos($saved.X, $saved.Y) | Out-Null
        $menu = $null
        for ($i = 0; $i -lt 20 -and -not $menu; $i++) {
            Start-Sleep -Milliseconds 150
            foreach ($w in $root.FindAll($S::Children, [System.Windows.Automation.Condition]::TrueCondition)) { if ($w.Current.ClassName -eq '#32768' -and $w.Current.ProcessId -eq $sweep.Id) { $menu = $w } }
        }
        if (-not $menu) { 'STOP: Sweepling menu did not open'; exit 1 }
        $h = [System.IntPtr]$menu.Current.NativeWindowHandle
        [W.U]::PostMessage($h, 0x0100, [System.IntPtr]0x26, [System.IntPtr]::Zero) | Out-Null
        Start-Sleep -Milliseconds 300
        [W.U]::PostMessage($h, 0x0100, [System.IntPtr]0x0D, [System.IntPtr]::Zero) | Out-Null
        'chose Quit Sweepling'
    }
}
