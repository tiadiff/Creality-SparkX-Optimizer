# Creality Print 7.3 - Speed & Stability Optimizer (SparkX Suite)

A high-performance optimization utility and crash fixer for **Creality Print 7.3** (`v7.3.1.6378`).

Eliminates startup lag, fixes the KLSIM kinematic solver divergence bug that caused 100% CPU thread lockups and multi-gigabyte memory leaks, purges orphaned WebView2 profiles, and optimizes system resource allocation.

---

## Technical Background: Why Creality Print 7.3 Was Slow & Freezing

In-depth reverse engineering of Creality Print 7.3 crash dumps and debug logs identified four critical architectural bottlenecks:

### 1. The Startup Lag: Embedded Chromium Home Page
* On launch, Creality Print initiates a local HTTP server on port `13667` and starts Microsoft Edge WebView2.
* It spawns over 15 `msedgewebview2.exe` helper processes to render the online Creality Cloud community feed, ads, and store before presenting the 3D slicing workspace.
* In addition, it reaches out to `api.crealitycloud.com` (`SyncUserPresets`) to query and download remote profiles, introducing network latency delays of 5 to 15 seconds.

### 2. The Slicer Divergence Bug: KLSIM `itersolve SC` diverged (`-nan(ind)`)
* In `SPARKX i7 0.4 nozzle.json`, the configuration parameter `"xy_step_dist": "0.0121875"` was enabled, activating Klipper's step compression solver (`itersolve_sc`) inside `CrealityPrint_Slicer.DLL`.
* At the same time, machine G-code profiles inject helical spiral arc commands:
  ```gcode
  G2 Z{z_after_toolchange + 0.4} I0.86 J0.86 P1 F10000
  M5001 G2 Z{layer_z + 0.4} I0.86 J0.86 P1 F20000
  ```
* KLSIM's Cartesian step compression solver cannot solve simultaneous XY circular arcs with Z helical lift and step compression. The solver mathematical approximation diverges, emitting:
  ```text
  [KLSIM] ERROR itersolve SC diverged: start=-nan(ind) end=-nan(ind) ... target=-12143.5 step_dist=0.0121875 line=12632
  ```
* Once coordinates become `NaN`, the elapsed time estimator overflows (`TOTAL END elapsed=484969041ms` ~ 134 hours) and all 16 CPU threads get stuck in an infinite busy-spin loop at 100% CPU, or the LOD generator attempts to allocate over 74 GB of virtual memory (`RequestedNewSize=74476407783` -> `std::bad_alloc` crash).

### 3. Background Slicing Race Condition (`auto_calculate: true`)
* With automatic background calculation enabled, any UI interaction or setting change triggers a slice job cancellation while worker threads are busy in KLSIM or infill calculation.
* This causes race conditions, thread deadlocks, and memory corruption (`0xc0000005` in `CrealityPrint_Slicer.DLL` or `0xc0000374` heap corruption in `ntdll.dll`).

### 4. 4.5+ GB of Orphaned WebView2 Profiles
* Located in `%LOCALAPPDATA%\Temp\slicer_webview_userdata`, Creality Print fails to reuse the primary profile if locked or corrupted (`active profile is occupied; using fallback profile`).
* Over time, dozens of duplicate profile directories accumulate (over 4.5 GB of dead cache), causing disk fragmentation and additional startup delays.

---

## What Creality-SparkX Optimizer Does

### 1-Click Automated Fixes
* **Instant Startup**: Configures Creality Print to bypass the Chromium home page and load directly into the 3D slicing plater ("Prepare" workspace).
* **Kinematics & Helical Arc Patch**:
  * Sets `"xy_step_dist": "0"`, disabling the unstable step compression solver.
  * Converts helical `G2 ... P1` spiral lifts into clean, linear `G1 Z...` moves in toolchanges, timelapse, and end-of-print G-code.
* **Slicer Race Condition Elimination**: Disables background `auto_calculate`, so slicing only runs when requested, completely preventing UI freezes and thread deadlocks.
* **Deep Cache Cleaner**: Purges all orphaned `slicer_webview_userdata` directories and stale `crealityprint_model` slices.
* **Cloud Boot-Check Bypass**: Disables automatic preset downloading from `api.crealitycloud.com` on boot (manual sync remains accessible).
* **DirectX High-Performance GPU Enforcement**: Enforces `GpuPreference=2;` for `CrealityPrint.exe` in Windows Registry.
* **Automated Safety Backups**: Creates a timestamped backup in `%APPDATA%\Creality\Creality Print\7.3\SparkX_Backups` before modifying any files, with a 1-click restore feature.

---

## System Requirements
* Windows 10 or Windows 11 (64-bit)
* .NET 8.0 Runtime (installed with Windows Desktop Runtime)
* Creality Print 7.3 (`v7.3.1.6378` or later)

---

## How to Run

### GUI Mode
1. Run `Avvia_SparkX_Optimizer.bat` or launch `SparkXOptimizer.exe`.
2. Inspect the detected status indicators (App Status, Startup Mode, Stability, Junk Size).
3. Click **1-CLICK FULL OPTIMIZATION**.
4. Once completed, click **Launch Creality Print**.

### Headless / Silent CLI Mode
You can integrate the optimizer into batch files, setup scripts, or shortcuts using the silent switch:
```cmd
SparkXOptimizer.exe --optimize
```
or
```cmd
SparkXOptimizer.exe --silent
```

---

## Changelog

### Version 1.0.1
* Added Cloud Presets Sync dialog and manual sync guidance.
* Added Windows Kernel Permanent High Priority integration via Image File Execution Options (IFEO).
* Added automated High Priority Desktop shortcut creator (Creality Print High Priority.lnk).
* Added real-time priority status detection in dashboard.

### Version 1.0.0
* Initial release of Creality-SparkX Optimizer.
* Added 1-Click full system optimization and automated backup mechanism.
* Implemented KLSIM kinematics patch for SPARKX and Creality machine profiles.
* Implemented WebView2 cache purger for slicer_webview_userdata.
* Added Creality.conf tuning for instant startup and background slice protection.
* Added High-Performance GPU registration.
* Added silent CLI mode for automated workflows.

