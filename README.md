# LiveWallpaper

Windows向けの「動く壁紙 + デスクトップ時計」アプリです。

## 目標

- MP4 / 画像をデスクトップ背景として表示
- 秒まで表示できる時計オーバーレイ
- 時計の位置・フォント・サイズ・透明度を変更
- Windows起動時に自動開始
- 全画面アプリ実行中は描画を一時停止
- 複数モニター対応
- 将来はWeb壁紙・編集機能・追加ウィジェットへ拡張
- Microsoft Store / Steamでの販売を想定

## 技術構成

- C#
- .NET 10
- WinUI 3
- Windows App SDK 2.5.1
- Win32 API（WorkerWへの配置など）

## 開発方針

`main` は安定版、`develop` は日常開発用です。機能開発は `feature/*` ブランチから行います。

最初の技術検証は「デスクトップ背景へウィンドウを配置し、`HH:mm:ss` の時計を表示する」です。

詳しくは `docs/ARCHITECTURE.md` と `docs/ROADMAP.md` を参照してください。
