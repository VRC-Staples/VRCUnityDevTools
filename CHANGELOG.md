# Changelog

All notable changes to this project will be documented in this file.

This project follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- README and package usage documentation updates are now tracked in release notes.
  - [`f51b91b`](https://github.com/VRC-Staples/VRCUnityDevTools/commit/f51b91b503b445462755533382f519f25f7289eb)
  - [`630b68c`](https://github.com/VRC-Staples/VRCUnityDevTools/commit/630b68cac1632c06964277737c192343140b28cc)

### Changed

- VCC/VPM listing generation now uses the repository-root `source.json` when rebuilding listing metadata.
  - [`3cb46a4`](https://github.com/VRC-Staples/VRCUnityDevTools/commit/3cb46a42585f38b5fb3d315de32e7ca3d33f866e)
- VPM listing configuration now includes source metadata for banner and listing generation.
  - [`3355e0f`](https://github.com/VRC-Staples/VRCUnityDevTools/commit/3355e0f609c1d626350d5f747c4b04a64a8ee590)
- VPM banner subtitle wording has been generalized and updated for broader readability.
  - [`5c58162`](https://github.com/VRC-Staples/VRCUnityDevTools/commit/5c5816273ad5a13b37a5c9980d808dde9659abba)

### Fixed

- Refined and updated VPM page banner copy.
  - [`1be7356`](https://github.com/VRC-Staples/VRCUnityDevTools/commit/1be7356f795cf9aeab60686d7d107ec7ce03d13ea)
  - [`27c0e7f`](https://github.com/VRC-Staples/VRCUnityDevTools/commit/27c0e7f795cf9aeab60686d7d107ec7ce03d13ea)

## [1.0.1]

- Included VPM listing URL as a markdown link in README documentation.
  - [`fda250e`](https://github.com/VRC-Staples/VRCUnityDevTools/commit/fda250e3f89f7f7c6dc1de8a3fcb9f7bf3f3d3f2)

## [1.0.0]

Initial standalone release of VRC Unity Dev Tools.

### Added (1.0.0)

- Extracted the tools into a standalone Unity package from AvatarSettingsManager-Lite.
- Split the editor code into the `Staples.DevTools.Editor` assembly.
- Added a project binding inspector that logs manifest and embedded package bindings for the current Unity project.
- Added a local or embedded package switcher for VCC-style package workflows across discovered VRChat projects.
- Added a synced expression parameter inspector for reviewing which avatar parameters are actually network synced and how much sync they use.

[Unreleased]: https://github.com/VRC-Staples/VRCUnityDevTools/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/VRC-Staples/VRCUnityDevTools/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/VRC-Staples/VRCUnityDevTools/releases/tag/v1.0.0
