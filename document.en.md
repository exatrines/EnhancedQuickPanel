# Enhanced Quick Panel — Documentation

**English** | [日本語](document.ja.md)

A customizable quick panel overlay for FFXIV ([Dalamud](https://github.com/goatcorp/Dalamud) plugin).

Show an overlay instead of the game's native quick panel, and build your own panels of actions, items, macros, and text commands.

<img src=assets/en/overlay.png style='display: block; margin: auto; max-width: 600px'>

## Features

- **Flexible slots** – Fill each slot with an action, item, macro, or a custom text/chat command.
- **Multiple pages** – Organize slots into as many pages as you want and switch between them with a click popup or the mouse wheel.
- **Drag & drop editing** – Drag actions and items directly from the game hotbars or inventory onto slots, and drag slots to swap them.
- **Icon picker** – Choose from in-game icons across every category, or import your own images from a URL as custom icons.
- **Import from the native quick panel** – Copy any page of the game's built-in quick panel into a new page.
- **Share via clipboard** – Import and export page content and styles as text, so you can back them up or share them with others.
- **Style presets** – Ship-ready `White` / `Gray` / `Black` presets, plus full control over colors, sizes, spacing, frames, tooltips, and overlay labels (cooldown, charges, quantity).
- **Native replacement** – Optionally show the overlay instead of the native quick panel when `/quickpanel` is used.
- **Localization** – English and Japanese UI.

## Repository

```
https://raw.githubusercontent.com/exatrines/DalamudPlugins/refs/heads/main/pluginmaster.json
```

## Usage

### Commands

| Command | Action |
| --- | --- |
| `/enhancedquickpanel` | Toggle the quick panel overlay. |
| `/enhancedquickpanel settings` | Toggle the settings window. |
| `/eqp` | Alias for `/enhancedquickpanel`. |
| `/eqp settings` | Alias for `/enhancedquickpanel settings`. |

### Editing panels

Right-click the overlay to open the context menu, then choose **Edit** to enter edit mode. In edit mode you can:

- Select a slot to edit its content (action, item, macro, or text command).
- Drag actions/items from the game hotbars or inventory onto a slot.
- Drag one slot onto another to swap them.
- Add, rename, remove, and reorder pages from the side panel.

<img src=assets/en/edit-mode.png style='display: block; margin: auto; max-width: 600px'>

## Customization

Open the settings window (`/eqp settings`) to configure the plugin.

### Settings tab

- Choose whether to show the overlay instead of the native quick panel for `/quickpanel`.
- Toggle the page selector popup, the edit button, and the empty-slot frame.
- Pick which context menu entries are visible.

### Style tab

- Apply a built-in preset (`White` / `Gray` / `Black`) or import/export a style via the clipboard.
- Adjust layout (slot size, spacing, padding), window and border colors, slot backgrounds, tooltips, and overlay label styling.

<table style='display: block; margin: auto; max-width: 800px'>
    <tr>
        <td><img src=assets/en/settings.png></td>
        <td><img src=assets/en/style.png></td>
    </tr>
</table>

## More

### Context menu

Right-clicking the overlay opens a menu whose entries you can choose to show.

- **Settings** – Open the settings window.
- **Import from clipboard** / **Export to clipboard** – Share the current page's slots.
- **Import from native quick panel** – Copy a native quick panel page into a new page.
- **Edit** – Toggle edit mode.
- **Close** – Hide the overlay.

<img src=assets/en/contextmenu.png style='display: block; margin: auto; max-width: 600px'>

### Icon picker

In the icon picker, in addition to the built-in icons, you can add your own icons by pasting an image URL (PNG / JPG / GIF / WebP). Imported icons are stored locally and can be reused across any slot.

<table style='display: block; margin: auto; max-width: 800px'>
    <tr>
        <td><img src=assets/en/icon-picker.png></td>
        <td><img src=assets/en/icon-picker-custom.png></td>
    </tr>
</table>

### Native quick panel import window

The native quick panel import window lets you create a panel from FFXIV's built-in quick panel.

<img src=assets/en/import-native-panel.png style='display: block; margin: auto; max-width: 600px'>

