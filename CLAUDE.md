# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What it is

`Grayscaler` is a system tray application built in .NET 8 / Windows Forms that applies a real-time grayscale effect over **a specific window chosen by the user** (not the whole screen). The user selects the target window by clicking on it, and the app draws a gray overlay on top that follows its position, size, and state.

It is Windows-specific: it relies on P/Invoke to `user32.dll` and the Magnification API (`Magnification.dll`).

## Commands

Everything runs from the `Grayscaler/` directory:

```powershell
dotnet build          # build
dotnet run            # build and run (launches the tray app)
```

There is no test project or linter configured.

## Architecture

The flow spans three pieces that only make sense together:

- **`Program.cs`** — entry point. Configures PerMonitorV2 DPI and starts `Application.Run(new AppController())`. There is no visible main window; the entire UI lives in the tray.

- **`AppController.cs`** (`ApplicationContext`) — the brain. Holds the `NotifyIcon` with its context menu, the target `HWND` (`_targetHwnd`), and a 30 ms `Timer` (`_trackTimer`) that syncs the overlay with the target window on every tick. Window selection uses a **low-level global mouse hook** (`WH_MOUSE_LL`): on left click, it captures the `HWND` under the cursor (`WindowFromPoint` + `GetAncestor(GA_ROOT)`), consumes the click by returning `1`, and uninstalls the hook.

- **`OverlayForm.cs`** (`Form`) — the gray window. It is borderless, layered, mouse-transparent, and non-activatable (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`). It hosts a native child control of class `"Magnifier"` (created via `CreateWindowEx`) to which `MagSetColorEffect` is applied with a grayscale matrix. The gray effect is produced by the Magnifier recapturing the target's region.

- **`Native.cs`** — all Win32 constants, structs (`RECT`, `POINT`, `MSLLHOOKSTRUCT`, `MAGCOLOREFFECT`), and P/Invoke declarations. `MagColorEffectGrayscale()` returns the 5x5 effect matrix.

## Non-obvious details (critical when editing)

These points have extensive comments in the code because they are fragile; respect them:

- **Overlay handle forced at startup.** `AppController` accesses `_overlay.Handle` in the constructor to create the handle ahead of time. Without this, the first `BeginInvoke` from the mouse hook would throw `InvalidOperationException` (the handle would only be created on the first `Show()`, which happens after selection).

- **The Magnifier does NOT auto-update.** `MagSetWindowSource` (`RefreshSource`) must be called on **every tick** of the timer, not only when the rect changes. If it is only refreshed on position changes, the gray content freezes.

- **Z-order: stay just above the target without being global topmost.** In `SyncOverlay`, to place the overlay above the target it is inserted after the window directly above it (`GetWindow(target, GW_HWNDPREV)`), not after the target itself (that would leave it below). If that window is already the overlay, nothing is done.

- **Magnification lifecycle.** `MagInitialize` is called in the `OverlayForm` constructor and `MagUninitialize` in `OnFormClosed`. The Magnifier control is created in `OnHandleCreated`.

- **State tracking in the timer.** `TrackTimer_Tick` detects whether the target disappeared (`IsWindow`) and resets state, or whether it is minimized (`IsIconic`) and hides the overlay.
