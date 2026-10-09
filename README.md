<p align="center">
  <img src="EnhancedQuickPanel/Data/plugin-icon.png" alt="Enhanced Quick Panel icon" width="128" height="128">
</p>

<h1 align="center">Enhanced Quick Panel</h1>

<p align="center">
  English | <a href="docs/README.ja.md">日本語</a>
</p>

<p align="center">
  <a href="https://github.com/exatrines/EnhancedQuickPanel/releases/latest">
    <img src="https://img.shields.io/github/v/release/exatrines/EnhancedQuickPanel?label=Release&amp;labelColor=F280B6&amp;color=FFFFFF&amp;style=flat&amp;sort=date&amp;display_name=tag" alt="Release">
  </a>
  <a href="CHANGELOG.md">
    <img src="https://img.shields.io/badge/Changelog-view-FFFFFF?labelColor=F280B6&amp;style=flat" alt="Changelog">
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/License-AGPL--3.0--or--later-FFFFFF?labelColor=F280B6&amp;style=flat" alt="AGPL-3.0-or-later">
  </a>
</p>

<p align="center">
  <img src="docs/screenshots/en/hero-1280x720.png" alt="Quick panel overlay">
</p>

Enhanced Quick Panel is a Dalamud plugin that shows a customizable overlay instead of the game’s native quick panel.

Fill slots with actions, items, macros, or text commands, spread them across pages, and style the panel. Drag from hotbars or inventory while editing. Optionally replace the native panel when `/quickpanel` is used.

See the [detailed manual](docs/document.en.md) for settings screenshots and editing notes.

## Install

1. Run `/xlsettings` and open the **Experimental** tab
2. Add this URL under **Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/exatrines/DalamudPlugins/refs/heads/main/pluginmaster.json
```

3. Run `/xlplugins` and install **Enhanced Quick Panel**

## Features

- **Flexible slots** — action, item, macro, or a custom text/chat command in each slot
- **Expandable layout** — native 5×5 (1×1) up to 2×2 blocks; size is shared by every page
- **Multiple pages** — switch pages from a click popup or the mouse wheel
- **Drag & drop editing** — drag from hotbars or inventory onto slots; drag slots to swap
- **Icon picker** — in-game icons, or images imported from a URL
- **Native import** — copy a page from the game’s built-in quick panel
- **Clipboard share** — import and export page content and styles as text
- **Style presets** — `White` / `Gray` / `Black`, plus colors, sizes, frames, and overlay labels
- **Native replacement** — optionally show the overlay instead of the native panel on `/quickpanel`
- **i18n** — English and Japanese UI strings

## Commands

| Command | Description |
| --- | --- |
| `/enhancedquickpanel` | Toggle the overlay |
| `/enhancedquickpanel settings` | Toggle plugin settings |
| `/eqp` | Alias for `/enhancedquickpanel` |
| `/eqp settings` | Alias for `/enhancedquickpanel settings` |

## For developers

1. `git submodule update --init --recursive`
2. Build: `dotnet build EnhancedQuickPanel.sln -c Release -p:Platform=x64`
3. Point Dalamud’s **dev plugin** path at `EnhancedQuickPanel/bin/Release/`
4. Enable **Enhanced Quick Panel** in the plugin installer (dev)

[MirageUI](https://github.com/exatrines/MirageUI) is included as a git submodule for the shared UI kit.

## Contributing

Contributions are always welcome! Please see the [contribution guide](CONTRIBUTING.md).
