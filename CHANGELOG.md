# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.7.0] - 2026-09-17

### Changed

- The settings window uses the latest MirageUI. Open the plugin page from the title bar or the sidebar icon (Discord and Support). The left-column footer links are gone
- Settings opens on the Settings tab when nothing is selected

## [1.0.6.0] - 2026-09-16

### Fixed

- Macro slot icons are resolved once and reused, so an overlay full of macros is cheaper to draw. Editing the slot or changing zones refreshes them

## [1.0.5.0] - 2026-09-16

### Changed

- Plugin shortcut icons now follow the Dalamud installer: official plugins from Dip17, third-party plugins from IconUrl, and only development installs from the plugin folder. Each icon is downloaded once and kept locally

### Added

- A notification and a short overlay/picker hint while plugin icons are downloading

## [1.0.4.0] - 2026-09-15

### Fixed

- Plugin shortcut slots reuse the installed plugin's icon file instead of asking Dalamud for it every frame

## [1.0.3.0] - 2026-09-15

### Fixed

- The plugin picker only draws visible rows, so choosing a plugin is cheaper with many installs
- An expanded overlay refreshes action usability less often and reuses game icon lookups
- Slot icons no longer throw when Dalamud disposes a texture wrap

## [1.0.2.0] - 2026-09-15

### Changed

- Plugin repository install and update links now point at this release's zip instead of `latest`

### Fixed

- Drawing an expanded overlay no longer rebuilds the installed-plugin list for every plugin slot each frame

## [1.0.1.0] - 2026-09-14

### Added

- Plugin slot editor lists installed plugins with icons and search (name, author, or internal name). Same-named installs are separate rows
- Plugin slots remember the chosen install, so left / right / middle-click stay on that copy when another plugin shares the same internal name

### Changed

- Plugin listing in the slot editor is always the search list (the combo picker is gone)
- Plugin icon on the pink frame used for DalamudPlugins

### Fixed

- Enabling a plugin is refused when another copy of the same internal name is already loaded
- A slot no longer silently switches to a different copy if the chosen install is missing

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

[Unreleased]: https://github.com/exatrines/EnhancedQuickPanel/compare/v1.0.7.0...HEAD
[1.0.7.0]: https://github.com/exatrines/EnhancedQuickPanel/compare/v1.0.6.0...v1.0.7.0
[1.0.6.0]: https://github.com/exatrines/EnhancedQuickPanel/compare/v1.0.5.0...v1.0.6.0
[1.0.5.0]: https://github.com/exatrines/EnhancedQuickPanel/compare/v1.0.4.0...v1.0.5.0
[1.0.4.0]: https://github.com/exatrines/EnhancedQuickPanel/compare/v1.0.3.0...v1.0.4.0
[1.0.3.0]: https://github.com/exatrines/EnhancedQuickPanel/compare/v1.0.2.0...v1.0.3.0
[1.0.2.0]: https://github.com/exatrines/EnhancedQuickPanel/compare/v1.0.1.0...v1.0.2.0
[1.0.1.0]: https://github.com/exatrines/EnhancedQuickPanel/compare/v1.0.0.0...v1.0.1.0
[1.0.0.0]: https://github.com/exatrines/EnhancedQuickPanel/compare/v0.1.3.0...v1.0.0.0
[0.1.3.0]: https://github.com/exatrines/EnhancedQuickPanel/releases/tag/v0.1.3.0
