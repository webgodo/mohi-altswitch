#Requires AutoHotkey v2.0
#SingleInstance Force

; Alt+` cycles through the windows of the active app; Alt+Shift+` goes backwards.
; Hold Alt for a moment and a preview strip appears (Esc cancels); release Alt to switch.
; A quick tap switches straight away with no popup.
;
; "Same app" means "same executable", not "same taskbar group". Chrome gives every
; profile its own taskbar identity, but every profile is still chrome.exe, so all
; profiles are reachable from here.

SetWinDelay(-1)

ShowDelay := 200        ; ms Alt must stay down before the preview appears
TileColor := "2B2B2B"
AccentColor := "0078D4"

list := []              ; the app's windows, most recently used first (list[1] is where you started)
sel := 1
active := false
showAt := 0
popup := ""

!sc029::Step(1)         ; sc029 is the ` / ~ key on any keyboard layout
!+sc029::Step(-1)

#HotIf active
*Esc::Finish(true)
#HotIf

Step(dir) {
    global list, sel, active, showAt
    if !active {
        cur := WinExist("A")
        if !cur
            return
        list := AppWindows(cur)
        if list.Length < 2
            return
        sel := 1
        active := true
        showAt := A_TickCount + ShowDelay
        SetTimer(Tick, 20)
    }
    sel := Mod(sel - 1 + dir + list.Length, list.Length) + 1
    if popup
        MoveHighlight()
}

Tick() {
    if !GetKeyState("Alt")
        Finish()
    else if !popup && A_TickCount >= showAt
        ShowPopup()
}

Finish(cancel := false) {
    global active
    SetTimer(Tick, 0)
    HidePopup()
    active := false
    if cancel || sel = 1 || !WinExist(list[sel])
        return
    if WinGetMinMax(list[sel]) = -1
        WinRestore(list[sel])
    WinActivate(list[sel])
}

ShowPopup() {
    global popup
    s := A_ScreenDPI / 96
    cw := Round(250 * s), ch := Round(180 * s)      ; one cell
    m := Round(6 * s), p := Round(8 * s)            ; gap around a tile, padding inside it
    titleH := Round(20 * s)
    WorkAreaOf(list[1], &ml, &mt, &mr, &mb)
    n := list.Length
    cols := Max(1, Min(n, Floor((mr - ml) * 0.9 / cw)))
    popupW := cols * cw, popupH := Ceil(n / cols) * ch   ; (variable names are case-insensitive: don't clash with w/h below)

    g := Gui("+AlwaysOnTop -Caption +ToolWindow +E0x08000000", "SameAppSwitcherPopup")   ; E0x08000000 = never takes focus
    g.BackColor := "1C1C1C"
    g.MarginX := 0, g.MarginY := 0
    g.SetFont("s9 cFFFFFF", "Segoe UI")
    hl := g.Add("Text", "x0 y0 w1 h1 Background" AccentColor)   ; selection ring, sits behind the tiles
    cells := [], thumbs := []
    for i, hwnd in list {
        x := Mod(i - 1, cols) * cw + m
        y := ((i - 1) // cols) * ch + m
        w := cw - 2 * m, h := ch - 2 * m
        cells.Push([x, y, w, h])
        g.Add("Text", Format("x{} y{} w{} h{} Background{}", x, y, w, h, TileColor))
        g.Add("Text", Format("x{} y{} w{} h{} Background{} +0x4200", x + p, y + h - p - titleH, w - 2 * p, titleH, TileColor), WinGetTitle(hwnd))
        th := AddThumbnail(g.Hwnd, hwnd, x + p, y + p, w - 2 * p, h - 3 * p - titleH)
        if th
            thumbs.Push(th)
    }
    DllCall("dwmapi\DwmSetWindowAttribute", "Ptr", g.Hwnd, "UInt", 33, "UInt*", 2, "UInt", 4)   ; rounded corners (Win11)
    popup := {gui: g, hl: hl, cells: cells, thumbs: thumbs}
    MoveHighlight()
    g.Show(Format("NoActivate x{} y{} w{} h{}", ml + ((mr - ml) - popupW) // 2, mt + ((mb - mt) - popupH) // 2, popupW, popupH))
}

MoveHighlight() {
    c := popup.cells[sel]
    popup.hl.Move(c[1] - 3, c[2] - 3, c[3] + 6, c[4] + 6)
    popup.hl.Redraw()
}

HidePopup() {
    global popup
    if !popup
        return
    for th in popup.thumbs
        DllCall("dwmapi\DwmUnregisterThumbnail", "Ptr", th)
    popup.gui.Destroy()
    popup := ""
}

; Live DWM preview of `src`, scaled to fit (never enlarged) and centred in the box. Returns the thumbnail handle or 0.
AddThumbnail(dest, src, bx, by, bw, bh) {
    th := 0
    if DllCall("dwmapi\DwmRegisterThumbnail", "Ptr", dest, "Ptr", src, "Ptr*", &th) != 0
        return 0
    sz := Buffer(8, 0)
    DllCall("dwmapi\DwmQueryThumbnailSourceSize", "Ptr", th, "Ptr", sz)
    sw := NumGet(sz, 0, "Int"), sh := NumGet(sz, 4, "Int")
    if sw < 1 || sh < 1 {
        DllCall("dwmapi\DwmUnregisterThumbnail", "Ptr", th)
        return 0
    }
    k := Min(bw / sw, bh / sh, 1)
    w := Round(sw * k), h := Round(sh * k)
    l := bx + (bw - w) // 2, t := by + (bh - h) // 2
    props := Buffer(48, 0)                                   ; DWM_THUMBNAIL_PROPERTIES
    NumPut("UInt", 0x1 | 0x4 | 0x8, props, 0)                ; destination rect + opacity + visible
    NumPut("Int", l, "Int", t, "Int", l + w, "Int", t + h, props, 4)
    NumPut("UChar", 255, props, 36)
    NumPut("Int", 1, props, 40)
    DllCall("dwmapi\DwmUpdateThumbnailProperties", "Ptr", th, "Ptr", props)
    return th
}

; Work area of the monitor the window is mostly on (primary if it can't be placed).
WorkAreaOf(hwnd, &l, &t, &r, &b) {
    try {
        WinGetPos(&x, &y, &w, &h, hwnd)
        cx := x + w // 2, cy := y + h // 2
        Loop MonitorGetCount() {
            MonitorGetWorkArea(A_Index, &l, &t, &r, &b)
            if cx >= l && cx < r && cy >= t && cy < b
                return
        }
    }
    MonitorGetWorkArea(MonitorGetPrimary(), &l, &t, &r, &b)
}

; The active window first, then the app's other windows in z-order (most recent first).
AppWindows(cur) {
    app := AppId(cur)
    owner := DllCall("GetAncestor", "Ptr", cur, "UInt", 3, "Ptr")   ; GA_ROOTOWNER: skip the window a dialog belongs to
    wins := [cur]
    for hwnd in WinGetList() {
        if hwnd != cur && hwnd != owner && IsSwitchable(hwnd) && AppId(hwnd) = app
            wins.Push(hwnd)
    }
    return wins
}

AppId(hwnd) {
    try path := WinGetProcessPath(hwnd)
    catch
        return ""
    if !(path ~= "i)\\ApplicationFrameHost\.exe$")
        return path
    ; Store apps all share ApplicationFrameHost.exe; the real app owns the CoreWindow inside the frame.
    try {
        for ctl in WinGetControlsHwnd(hwnd) {
            if WinGetClass(ctl) = "Windows.UI.Core.CoreWindow"
                return WinGetProcessPath(ctl)
        }
    }
    return path
}

; Roughly the test Alt+Tab applies: titled, not a tool window, not owned, not cloaked
; (cloaked = on another virtual desktop, or a suspended Store app).
IsSwitchable(hwnd) {
    if WinGetTitle(hwnd) = ""
        return false
    if WinGetClass(hwnd) ~= "^(Progman|WorkerW|Shell_TrayWnd|Shell_SecondaryTrayWnd|XamlExplorerHostIslandWindow)$"
        return false
    ex := WinGetExStyle(hwnd)
    if ex & 0x08000000                                               ; WS_EX_NOACTIVATE (overlays, popups)
        return false
    isAppWindow := ex & 0x40000                                      ; WS_EX_APPWINDOW
    if (ex & 0x80) && !isAppWindow                                   ; WS_EX_TOOLWINDOW
        return false
    if !isAppWindow && DllCall("GetWindow", "Ptr", hwnd, "UInt", 4, "Ptr")   ; GW_OWNER
        return false
    cloaked := 0
    DllCall("dwmapi\DwmGetWindowAttribute", "Ptr", hwnd, "UInt", 14, "UInt*", &cloaked, "UInt", 4)
    return !cloaked
}
