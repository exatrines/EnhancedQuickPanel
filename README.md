# Enhanced Quick Panel

[日本語](README.ja.md)

![Quick panel overlay](docs/screenshots/en/overlay.png)

Enhanced Quick Panel is a Dalamud plugin that shows a customizable overlay instead of the game’s native quick panel.

Fill slots with actions, items, macros, or text commands, spread them across pages, and style the panel. Drag from hotbars or inventory while editing. Optionally replace the native panel when `/quickpanel` is used.

See the [detailed manual](document.en.md) for settings screenshots and editing notes.

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

1. Build: `dotnet build EnhancedQuickPanel.sln -c Release -p:Platform=x64`
2. Point Dalamud’s **dev plugin** path at `EnhancedQuickPanel/bin/Release/`
3. Enable **Enhanced Quick Panel** in the plugin installer (dev)

[MirageUI](https://github.com/exatrines/MirageUI) is included as a git submodule for the shared UI kit.

## License

[AGPL-3.0-or-later](LICENSE)
