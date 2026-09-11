# Enhanced Quick Panel — Documentation

**English** | [日本語](document.ja.md)

A customizable quick panel overlay for FFXIV ([Dalamud](https://github.com/goatcorp/Dalamud) plugin).

Show an overlay instead of the game's native quick panel, and build your own panels of actions, items, macros, and text commands.

<img src=docs/screenshots/en/overlay.png style='display: block; margin: auto; max-width: 600px'>

## Features

- **Flexible slots** – Fill each slot with an action, item, macro, or a custom text/chat command.
- **Expandable layout** – Grow the panel from the native 5×5 (1×1) up to 2×2 blocks (1×2, 2×1, or 2×2). The size is shared by every page.
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

<img src=docs/screenshots/en/edit-mode.png style='display: block; margin: auto; max-width: 600px'>

## Customization

Open the settings window (`/eqp settings`) to configure the plugin.

### Settings tab

- Choose whether to show the overlay instead of the native quick panel for `/quickpanel`.
- Choose the panel size (1×1 / 1×2 / 2×1 / 2×2). Existing slots keep their positions. Shrinking is blocked, with a warning, if it would delete configured slots.
- Toggle the page selector popup, the collapse button, the edit button, and the empty-slot frame.
- Pick which context menu entries are visible.

### Style tab

- Apply a built-in preset (`White` / `Gray` / `Black`) or import/export a style via the clipboard.
- Adjust layout (slot size, spacing, padding), window and border colors, slot backgrounds, tooltips, and overlay label styling.

<table style='display: block; margin: auto; max-width: 800px'>
    <tr>
        <td><img src=docs/screenshots/en/settings.png></td>
        <td><img src=docs/screenshots/en/style.png></td>
    </tr>
</table>

## More

### Context menu

Right-clicking the overlay opens a menu whose entries you can choose to show.

- **Settings** – Open the settings window.
- **Import from clipboard** / **Export to clipboard** – Share the current page's slots, including its width and height. A page that is wider or taller than the current layout is rejected; a smaller page pastes into the top-left and leaves the rest of the page unchanged.
- **Import from native quick panel** – Copy a native 5×5 quick panel page into the top-left of a new page. The current layout does not change.
- **Edit** – Toggle edit mode.
- **Collapse / expand** – Collapse the overlay to the expand button, or restore the panel. The same state is also toggled by the dedicated button on the overlay.
- **Close** – Hide the overlay.

<img src=docs/screenshots/en/contextmenu.png style='display: block; margin: auto; max-width: 600px'>

### Icon picker

In the icon picker, in addition to the built-in icons, you can add your own icons by pasting an image URL (PNG / JPG / GIF / WebP). Imported icons are stored locally and can be reused across any slot.

<table style='display: block; margin: auto; max-width: 800px'>
    <tr>
        <td><img src=docs/screenshots/en/icon-picker.png></td>
        <td><img src=docs/screenshots/en/icon-picker-custom.png></td>
    </tr>
</table>

### Native quick panel import window

The native quick panel import window lets you create a panel from FFXIV's built-in 5×5 quick panel. It is written into the top-left of a new page; a larger layout is left as-is around that block.

<img src=docs/screenshots/en/import-native-panel.png style='display: block; margin: auto; max-width: 600px'>

### Plugin shortcuts and collapsing

In edit mode, empty slots can be Text command, Dalamud, or Plugin. Dalamud offers the plugin installer, Dalamud settings, Dalamud Data, and Dalamud Console. These open Dalamud UI directly rather than sending `/xl*` chat commands. A Font Awesome icon is used until you pick another.

In edit mode, select a slot and change its type to Plugin, then select an installed plugin. Defaults: left click opens its main window, right click opens settings, and middle click enables or disables the plugin. Each button can independently open a window, run a command, toggle the plugin, or do nothing. Assign a plugin command when the plugin does not expose the requested window. Enhanced Quick Panel cannot enable or disable itself.

Slots use the plugin's own icon by default. The icon button opens the icon picker for game or custom icons; clearing the icon in the picker restores the plugin icon. If Dalamud does not already have that icon, it is downloaded from the plugin's icon URL into `icon/Plugins` under the custom-icon folder (one file per plugin, overwritten on download). A `?` appears when the icon is still unavailable. Disabled plugins stay on the panel in a dimmed state; hovering them appends (Disabled) to the tooltip. A sync overlay appears while a toggle is in progress.

The overlay header can show a collapse button, the current page, and the edit button. The dedicated button and the right-click menu both collapse or expand the panel, and this state is saved. Collapsing leaves the expand button as the remaining control. Use Shift + right click on plugin slots to open the panel management menu. Normal right click still opens that menu in edit mode.

These features were implemented from the public specification of [Dalamud Quick Launcher](https://github.com/elpapityo/DalamudQuickLauncher), with its developer's permission.

