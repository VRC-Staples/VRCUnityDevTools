# VRC Unity Dev Tools

[![CI](https://github.com/VRC-Staples/VRCUnityDevTools/actions/workflows/ci.yml/badge.svg)](https://github.com/VRC-Staples/VRCUnityDevTools/actions/workflows/ci.yml)

Unity editor utilities for VRChat, packaged as `com.staples.vrc-unity-dev-tools`.

## Overview

This package bundles a few focused editor tools for common VRChat workflow pain points that I have had so I decided to just build small tools to help:

- inspect how the current Unity project is binding packages
- switch installed VCC packages between local file dependencies and embedded copies across projects, allowing for quicker asset refreshes from local dev repos.
- inspect which avatar expression parameters are actually network synced

## Included Tools

### Show Current Project Binding

Menu path: `Tools/.Staples./Dev Tools/Show Current Project Binding`

Prints a summary of the current project's package bindings to the Unity console, including:

- `Packages/manifest.json` dependencies
- whether each dependency looks like a registry version, local `file:` dependency, or remote source
- embedded packages found under the project's `Packages` folder

This behavior is implemented in `Packages/com.staples.vrc-unity-dev-tools/Editor/ProjectBindingTool.cs:13-55`.

### Local or Embedded Package Switcher

Menu path: `Tools/.Staples./Dev Tools/Local or Embedded Package Switcher...`

Opens an editor window that scans VCC local package sources, shows switchable packages already relevant to the current project, then lets you update selected VCC projects to use:

- a local `file:` package dependency, or
- the embedded package copy in `Packages/<package-name>`

The switcher window flow lives in `Packages/com.staples.vrc-unity-dev-tools/Editor/VccPackageSwitcherWindow.cs:30-252`, package discovery lives in `Packages/com.staples.vrc-unity-dev-tools/Editor/VccPackageDiscovery.cs:11-167`, and manifest / embedded switching is handled in `Packages/com.staples.vrc-unity-dev-tools/Editor/VccPackageBindingService.cs:12-106`.

#### What it looks at

The switcher reads VRChat Creator Companion settings from Local AppData to find:

- user project roots
- local package folders
- direct local package entries

See `Packages/com.staples.vrc-unity-dev-tools/Editor/VccPackageDiscovery.cs:196-224`.

#### Embedded package handling

When switching from embedded to local, the tool can move the embedded package into a timestamped backup folder named `.dev-tools-package-backups` under the project root before applying the file dependency. When switching back to embedded, it can restore the latest backup if present. See `Packages/com.staples.vrc-unity-dev-tools/Editor/VccPackageBindingService.cs:30-43` and `Packages/com.staples.vrc-unity-dev-tools/Editor/VccPackageBindingService.cs:61-90`.

### Synced Param Inspector

Menu path: `Tools/.Staples./Dev Tools/Synced Param Inspector`

Opens a window for the selected `VRCAvatarDescriptor` and shows:

- the linked `VRCExpressionParameters` asset and its asset path
- total parameter count
- synced parameter count
- synced memory usage out of the 256-byte VRChat budget
- a sorted list of synced parameter names and value types

It also auto-picks a descriptor when you select any child object under an avatar in the scene. See `Packages/com.staples.vrc-unity-dev-tools/Editor/VRC/SyncedParamInspectorWindow.cs:32-177` and the data refresh logic in `Packages/com.staples.vrc-unity-dev-tools/Editor/VRC/SyncedParamInspectorWindow.cs:241-284`.

## Requirements

- Unity 2022.3.22f1 project, as declared in `Packages/com.staples.vrc-unity-dev-tools/package.json:4-5`
- VRChat SDK assemblies referenced by the editor asmdef:
  - `VRC.SDKBase`
  - `VRC.SDK3A`
  - `VRC.SDK3A.Editor`

See `Packages/com.staples.vrc-unity-dev-tools/Editor/Staples.DevTools.Editor.asmdef:2-19`.

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
│     └─ SyncedParamInspectorWindow.cs
├─ package.json
├─ README.md
├─ CHANGELOG.md
└─ LICENSE
```

The repository currently contains this package under `Packages/com.staples.vrc-unity-dev-tools`, and VPM listing assets are stored in `Website/`.

## Notes

- The editor menu only registers two top-level menu items from `DevToolsMenuItems.cs`, while the synced parameter inspector registers its own menu item directly from the inspector window class. See `Packages/com.staples.vrc-unity-dev-tools/Editor/DevToolsMenuItems.cs:5-17` and `Packages/com.staples.vrc-unity-dev-tools/Editor/VRC/SyncedParamInspectorWindow.cs:32-38`.
- The package is editor-only in practice, with the asmdef limited to the `Editor` platform. See `Packages/com.staples.vrc-unity-dev-tools/Editor/Staples.DevTools.Editor.asmdef:9-10`.


## VPM Listing (GitHub Pages)

You can add this repository to VCC using:
- VPM Listing URL: https://vrc-staples.github.io/VRCUnityDevTools/index.json

Pages for this listing are generated during the Release workflow from the `Website` folder.
