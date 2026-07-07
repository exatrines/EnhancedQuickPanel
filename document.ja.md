# Enhanced Quick Panel — ドキュメント

[English](document.en.md) | **日本語**

FFXIV用のカスタマイズ可能なクイックパネルオーバーレイ（[Dalamud](https://github.com/goatcorp/Dalamud) プラグイン）です。

ゲーム標準のネイティブクイックパネルの代わりにオーバーレイを表示し、アクション・アイテム・マクロ・テキストコマンドを自由に配置したパネルを作成することができます。

<img src=assets/ja/overlay.png style='display: block; margin: auto; max-width: 600px'>

## 機能

- **柔軟なスロット** – 各スロットにアクション・アイテム・マクロ・任意のテキスト／チャットコマンドを設定できます。
- **複数ページ** – スロットを好きなだけページに分けて整理し、ページ名クリックのポップアップやマウスホイールで切り替えられます。
- **ドラッグ＆ドロップ編集** – ゲームのホットバーやインベントリからアクション・アイテムを直接スロットへドラッグでき、スロット同士のドラッグで入れ替えも可能です。
- **アイコン選択** – 全カテゴリーのゲーム内アイコンから選べるほか、URL から画像を取り込んでカスタムアイコンとして使用できます。
- **ネイティブクイックパネルからインポート** – ゲーム標準のクイックパネルの任意のページを新しいページとして取り込めます。
- **クリップボード共有** – パネル内容やスタイルをテキストとしてインポート／エクスポートでき、バックアップや共有に使えます。
- **スタイルプリセット** – すぐ使える `White` / `Gray` / `Black` プリセットに加え、色・サイズ・間隔・枠・ツールチップ・オーバーレイ表示（リキャスト・スタック数・所持数）を細かく設定できます。
- **ネイティブ置き換え** – `/quickpanel` 実行時に、ネイティブクイックパネルの代わりにオーバーレイを表示するオプション。
- **多言語対応** – 日本語・英語の UI。

## リポジトリ

```
https://raw.githubusercontent.com/exatrines/DalamudPlugins/refs/heads/main/pluginmaster.json
```

## 使い方

### コマンド

| コマンド | 動作 |
| --- | --- |
| `/enhancedquickpanel` | クイックパネルオーバーレイの表示／非表示を切り替えます。 |
| `/enhancedquickpanel settings` | 設定ウィンドウの表示／非表示を切り替えます。 |
| `/eqp` | `/enhancedquickpanel` のエイリアスです。 |
| `/eqp settings` | `/enhancedquickpanel settings` のエイリアスです。 |

### パネルの編集

オーバーレイを右クリックしてコンテキストメニューを開き、**編集** を選ぶと編集モードに入ります。編集モードでは次のことができます。

- スロットを選択して内容（アクション・アイテム・マクロ・テキストコマンド）を編集する。
- ゲームのホットバーやインベントリからアクション・アイテムをスロットへドラッグする。
- スロット同士をドラッグして入れ替える。
- サイドパネルからページの追加・名前変更・削除・並べ替えを行う。

<img src=assets/ja/edit-mode.png style='display: block; margin: auto; max-width: 600px'>

## カスタマイズ

設定ウィンドウ（`/eqp settings`）を開いて各種設定を行います。

### 設定タブ

- `/quickpanel` 実行時にネイティブクイックパネルの代わりにオーバーレイを表示するかを選択。
- ページ選択ポップアップ、編集ボタン、空スロットの枠の表示を切り替え。
- コンテキストメニューに表示する項目を選択。

### スタイルタブ

- 組み込みプリセット（`White` / `Gray` / `Black`）を適用、またはスタイルをクリップボードでインポート／エクスポート。
- レイアウト（マスサイズ・間隔・余白）、ウィンドウや枠の色、スロット背景、ツールチップ、オーバーレイ表示のスタイルを調整。

<table style='display: block; margin: auto; max-width: 800px'>
    <tr>
        <td><img src=assets/ja/settings.png></td>
        <td><img src=assets/ja/style.png></td>
    </tr>
</table>

## その他

### コンテキストメニュー

オーバーレイの右クリックで、表示項目を選べるメニューが開きます。

- **設定** – 設定ウィンドウを開きます。
- **クリップボードからインポート** / **クリップボードにエクスポート** – 現在のページのスロットを共有します。
- **ネイティブクイックパネルからインポート** – ネイティブクイックパネルのページを新しいページとして取り込みます。
- **編集** – 編集モードを切り替えます。
- **閉じる** – オーバーレイを非表示にします。

<img src=assets/ja/contextmenu.png style='display: block; margin: auto; max-width: 600px'>

### アイコン選択画面

アイコン選択画面では、標準搭載のアイコンに加え、画像 URL（PNG / JPG / GIF / WebP）を貼り付けて独自のアイコンを追加できます。取り込んだアイコンはローカルに保存され、どのスロットでも再利用できます。

<table style='display: block; margin: auto; max-width: 800px'>
    <tr>
        <td><img src=assets/ja/icon-picker.png></td>
        <td><img src=assets/ja/icon-picker-custom.png></td>
    </tr>
</table>

### ネイティブクイックパネルインポート画面

ネイティブクイックパネルインポート画面ではFFXIV標準のクイックパネルからパネルを作成できます。

<img src=assets/ja/import-native-panel.png style='display: block; margin: auto; max-width: 600px'>


## ビルド

必要環境: 現在の Dalamud API レベルに対応した .NET SDK と、動作する Dalamud 開発環境。

```bash
dotnet build EnhancedQuickPanel/EnhancedQuickPanel.csproj -c Release
```

ビルド成果物（ローカライズファイルやスタイルプリセットを含む）は `EnhancedQuickPanel/bin/Release` に出力されます。

## クレジット

- [Dalamud](https://github.com/goatcorp/Dalamud)、[ECommons](https://github.com/NightmareXIV/ECommons)、MirageUI を使用しています。

## ライセンス

ライセンスの詳細はリポジトリを参照してください。
