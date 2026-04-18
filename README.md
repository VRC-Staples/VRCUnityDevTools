# VRC Unity Dev Tools

[![CI](https://github.com/VRC-Staples/VRCUnityDevTools/actions/workflows/ci.yml/badge.svg)](https://github.com/VRC-Staples/VRCUnityDevTools/actions/workflows/ci.yml)

Unity editor utilities for VRChat, packaged as `com.staples.vrc-unity-dev-tools` (current version `1.0.2`).

## Overview

VRC Unity Dev Tools is an **editor-only** Unity package that helps with common
VRChat development workflow tasks, including package binding inspection,
local/embedded package switching, synced avatar expression
parameter debugging, and avatar light cleanup for dark-mode scene prep.

## Included Tools

### Show Current Project Binding

**Menu:** `Tools/.Staples./Dev Tools/Show Current Project Binding`

Prints a summary of the current project's package bindings to the Unity console, including:

- `Packages/manifest.json` dependencies
- Dependency type (`registry`, local `file:` path, or remote)
- Embedded package bindings discovered under `Packages/<package-name>`

This behavior is implemented in:
`Packages/com.staples.vrc-unity-dev-tools/Editor/ProjectBindingTool.cs`.

### Local or Embedded Package Switcher

**Menu:** `Tools/.Staples./Dev Tools/Local or Embedded Package Switcher...`

Opens an editor window that scans VCC local package sources, lists packages relevant to the current project, and switches selected projects between:

- local dependency mode (`file:` path), and
- embedded package mode (`Packages/<package-name>`).

It can back up the current embedded package into `.dev-tools-package-backups` before switching to local mode, and restores the latest backup when returning to embedded.

Core logic for this feature lives in:
- `VccPackageSwitcherWindow.cs`
- `VccPackageDiscovery.cs`
- `VccPackageBindingService.cs`

### Synced Param Inspector

**Menu:** `Tools/.Staples./Dev Tools/Synced Param Inspector`

Shows network-synced expression parameter details for the selected `VRCAvatarDescriptor`:

- linked `VRCExpressionParameters` asset + asset path
- total parameter count and synced parameter count
- synced memory usage against the 256-byte budget
- sorted list of synced parameter names and value types

It can auto-select a descriptor when you select any child of an avatar in the scene.

### Dark Mode

**Menu:** `Tools/.Staples./Dev Tools/Dark Mode`

Opens an editor window for the selected `VRCAvatarDescriptor` that scans for removable light entries and lets you apply only the rows you keep selected:

- non-baked scene `Light` components under the avatar hierarchy
- VRCFury haptic socket components with `addLight` enabled
- VRCFury haptic plug components with `addDpsTipLight` enabled

When applied, the tool removes dynamic scene lights, disables supported VRCFury-generated light settings, and records the change through Unity undo operations.

## Requirements

- Unity 2022.3 (as defined in `package.json`)
- VRChat SDK references in editor asmdef:
  - `VRC.SDKBase`
  - `VRC.SDK3A`
  - `VRC.SDK3A.Editor`
- Editor-only usage (`includePlatforms`: `Editor`)

## Installation and Distribution

### VCC (recommended)

Use the GitHub Pages listing:

- GitHub Pages listing page: `https://vrc-staples.github.io/VRCUnityDevTools/`
- VPM listing JSON URL: `https://vrc-staples.github.io/VRCUnityDevTools/index.json`

### VCC Listing Assets

Listing-related files are in `Website/` and are generated during release.

## Package Structure

```text
Packages/com.staples.vrc-unity-dev-tools/
├─ Editor/
│  ├─ DevToolsMenuItems.cs
│  ├─ ProjectBindingTool.cs
│  ├─ VccLocalPackage.cs
│  ├─ VccPackageBindingService.cs
│  ├─ VccPackageDiscovery.cs
│  ├─ VccPackageSwitcherWindow.cs
│  ├─ Staples.DevTools.Editor.asmdef
│  └─ VRC/
│     ├─ DarkModeTool.cs
│     └─ SyncedParamInspectorWindow.cs
├─ package.json
├─ README.md
├─ CHANGELOG.md
└─ LICENSE
```

## Notes

- The package menu exposes two top-level menu items from `DevToolsMenuItems.cs`; the synced parameter inspector and dark mode tools each register their own menu entries.
- The repo ships with no runtime/gameplay code; this is strictly editor tooling.

## CI / Validation

The repository includes GitHub Actions workflows for:

- commit identity validation
- linting
- package validation
- release and nightly listing deployment

See `.github/workflows/ci.yml`, `.github/workflows/release.yml`, and `.github/workflows/scheduled.yml`.

## License

This package is licensed under **GPL-3.0**.
