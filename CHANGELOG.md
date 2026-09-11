# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.0.0] - 2026-09-11

### Added

- Plugin shortcut slots. Left-click opens Main UI (or Config if there is no Main UI); right-click opens Config (or Main). Middle-click can enable or disable the plugin (not this plugin itself). Loaded plugins with neither Main nor Config UI are omitted from the picker. Settings control the middle-click toggle and whether right-click opens plugin settings instead of the slot menu
- Dalamud shortcut slots for the plugin installer, Dalamud settings, Data, and Log
- Overlay collapse from the header button and the context menu. The collapsed state is saved; only the expand control remains while collapsed
- Slot actions on the overlay context menu: execute, edit, delete (hold Shift), type-specific lists (`/macro`, `/action`, `/inventory`), and plugin window / settings / enable-disable
- Extra layouts besides native `5×5`: `5×10`, `10×5`, and `10×10`. The size is shared by every page
- Settings format `v1` (`ConfigVersion`). Existing `0.1.3.0` settings are migrated on load; a backup is written first. Unreadable or newer files abort plugin load instead of replacing them with defaults

### Changed

- Right-click a slot to show panel items under **Enhanced Quick Panel** and that slot’s actions under the slot name. Shift+right-click (or chrome) still opens the panel menu only
- Hold-and-move on chrome or a slot drags the overlay; a click without moving still clicks
- License is now AGPL-3.0-or-later

### Fixed

- Releasing the mouse over a different slot no longer runs that slot

## [0.1.3.0] - 2026-07-28

Customizable quick panel overlay with pages, native import, and style presets.

[Unreleased]: https://github.com/exatrines/EnhancedQuickPanel/compare/v1.0.0.0...HEAD
[1.0.0.0]: https://github.com/exatrines/EnhancedQuickPanel/compare/v0.1.3.0...v1.0.0.0
[0.1.3.0]: https://github.com/exatrines/EnhancedQuickPanel/releases/tag/v0.1.3.0
