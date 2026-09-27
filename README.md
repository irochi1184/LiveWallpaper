# LiveWallpaper

Windows向けの「動く壁紙 + デスクトップ時計」アプリです。プライマリ画面のデスクトップ背景に時計を表示し、設定画面から見た目と配置を変更できます。

## 起動方法

1. GitHub Actions の **Windows build** で成功した実行を開き、Artifacts の **LiveWallpaper-win-x64** をダウンロードします。
2. ZIPをフォルダーへすべて展開し、`LiveWallpaper.App.exe` を起動します。EXEだけを取り出さないでください。
3. **時計をデスクトップに表示** を押します。デスクトップアイコンの背面に暗い背景と `HH:mm:ss` が表示されます。
4. **時計を閉じる** で元の壁紙へ戻ります。設定画面の×ボタンは画面を隠して常駐を続けます。
5. トレイアイコンのクリック、またはEXEの再起動で設定画面を開けます。完全に終了するときは **アプリを終了**、またはトレイの右クリックメニューから終了します。

.NET / Windows App SDKを同梱した非MSIXのx64版です。管理者として起動する必要はありません。既存の壁紙設定は変更しません。

## 開発環境・ビルド

- Windows 10 1809以降 / Windows 11（実機検証対象はWindows 11）
- .NET 10 SDK
- C# / WinUI 3 / Microsoft.WindowsAppSDK 2.5.1
- Windows SDK BuildTools 10.0.28000.2705（NuGetから復元）

PowerShell:

```powershell
git clone https://github.com/irochi1184/LiveWallpaper.git
cd LiveWallpaper
git switch develop
dotnet build LiveWallpaper.sln -c Release -p:Platform=x64
dotnet run --project tests/LiveWallpaper.Windows.Tests -c Release
dotnet publish src/LiveWallpaper.App/LiveWallpaper.App.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true -o artifacts/LiveWallpaper-win-x64
.\artifacts\LiveWallpaper-win-x64\LiveWallpaper.App.exe
```

Visual Studioでは `LiveWallpaper.sln` を開き、スタートアッププロジェクトを `LiveWallpaper.App`、構成をx64にします。MSIXパッケージ化は不要です。

## 現在の動作

- デスクトップ構造を判定し、従来のWorkerW、または新しいWindows 11のProgman内のアイコン直下へ配置
- プライマリ画面全体へ配置（タスクバーの領域を含む）、親ウィンドウの原点を考慮
- デスクトップアイコンと通常のアプリより背面、フォーカスを奪わず表示
- 250msごとにOS時刻を読み、変化したときだけ時計を書き換え
- 表示中の再作成を防止、停止後に再表示可能
- 画面サイズ・親の位置を毎秒確認し再配置
- Explorerとの接続が失われた場合は停止し、再表示を案内
- 表示失敗時は時計を閉じて操作画面へエラーを表示
- タイマー停止・WorkerWから切り離し・ウィンドウ破棄を一括実施

PNG・JPEG画像または単色背景に時計を重ねられます。動画壁紙、複数画面すべてへの表示は今後の機能です。

## 背景の設定と二重起動防止

**画像を選ぶ** でPNG・JPEGを選び、プレビューで確認してから **時計をデスクトップに表示** を押します。表示中の変更もすぐに反映されます。

- 表示方法は「全面に広げる」「画像全体を表示」「引き伸ばす」の3種類
- 背景色は画像の余白や透明部分にも適用。「画像を外す」で単色に戻せます
- 画像の場所・表示方法・背景色を時計設定と一緒に保存し、次回起動時に復元
- 画像を移動・削除した場合は背景色で表示し、選び直しを案内
- アプリをもう一度起動すると、起動済みの設定画面を表示。最小化中なら復元

画像はコピーせず、選択したファイルを参照します。大きな画像は読み込み時に長辺を最大4096ピクセルへ縮小します。以前の時計設定はそのまま引き継ぎます。詳しくは [docs/IMAGE_WALLPAPER.md](docs/IMAGE_WALLPAPER.md) を参照してください。

## 時計の設定

- 秒表示、12/24時間表示
- 文字サイズ（24〜240）、文字色、不透明度（10〜100%）
- 中央・左上・右上・左下・右下と、画面端からの余白（0〜256）
- 設定画面の時計プレビュー、表示中の時計への即時反映
- 変更後の自動保存、次回起動時の復元、初期設定へのリセット

保存先は `%LOCALAPPDATA%\LiveWallpaper\settings.json`。書き込み成功後にファイルを置き換え、以前の内容を `settings.json.bak` に残します。読めないファイルでは初期設定で起動し、保存に失敗した場合は画面で再試行できます。アプリの再起動時に時計自体を自動表示する機能はまだありません。詳しくは [docs/CLOCK_SETTINGS.md](docs/CLOCK_SETTINGS.md) を参照してください。

## 検証

2026-09-27のWindows実機でPNG画像と秒付き時計の表示、画像設定の復元、最小化からの再表示を確認しました。常駐版では×による非表示、再起動による同一プロセスの画面復帰、明示的な終了も確認しています。トレイメニューの直接操作とExplorer再起動は未確認です。

画像設定の保存・復元・解除、従来形式との互換性もテストします。配布版を起動して、2回目の起動と同時起動でプロセスと操作ウィンドウが1つになること、通常終了できることをCIで確認します。

push（main/develop）とPRでWindows上のビルド、ネイティブウィンドウの統合テスト、配布フォルダーの生成を行います。テストは負の親座標、サイズ変更、属性、フォーカス、失敗時の復元、親消失、繰り返し終了、時計書式、設定の保存・復元・不正値補正・保存失敗時の保全を確認します。

CIでExplorerの壁紙表示そのものは保証できません。Windowsの実画面での確認手順と対応範囲は [docs/VERIFICATION.md](docs/VERIFICATION.md) を参照してください。WorkerWは公開APIではなく、Explorerのバージョンによって利用できないことがあります。

`develop` が開発ブランチです。`main` への反映はPRで行います。構成は [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)、今後の計画は [docs/ROADMAP.md](docs/ROADMAP.md) を参照してください。
