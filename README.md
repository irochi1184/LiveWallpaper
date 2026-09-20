# LiveWallpaper

Windows向けの「動く壁紙 + デスクトップ時計」アプリです。現在の到達点は、アプリのボタンからプライマリ画面のデスクトップ背景に秒付き時計を表示することです。

## 起動方法

1. GitHub Actions の **Windows build** で成功した実行を開き、Artifacts の **LiveWallpaper-win-x64** をダウンロードします。
2. ZIPをフォルダーへすべて展開し、`LiveWallpaper.App.exe` を起動します。EXEだけを取り出さないでください。
3. **時計をデスクトップに表示** を押します。デスクトップアイコンの背面に暗い背景と `HH:mm:ss` が表示されます。
4. **時計を閉じる** で元の壁紙へ戻ります。操作画面の×ボタンでも時計とアプリの両方が終了します。

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

- WorkerWを探して時計ウィンドウを子ウィンドウとして配置
- プライマリ画面全体へ配置（タスクバーの領域を含む）、親ウィンドウの原点を考慮
- デスクトップアイコンと通常のアプリより背面、フォーカスを奪わず表示
- 250msごとにOS時刻を読み、変化したときだけ時計を書き換え
- 表示中の再作成を防止、停止後に再表示可能
- 画面サイズ・親の位置を毎秒確認し再配置
- Explorerとの接続が失われた場合は停止し、再表示を案内
- 表示失敗時は時計を閉じて操作画面へエラーを表示
- タイマー停止・WorkerWから切り離し・ウィンドウ破棄を一括実施

現在は暗い単色背景です。既存の画像に透明な時計だけを重ねる機能ではありません。画像・動画壁紙、設定保存、複数画面すべてへの表示は今後の機能です。

## 検証

push（main/develop）とPRでWindows上のビルド、ネイティブウィンドウの統合テスト、配布フォルダーの生成を行います。テストは負の親座標、サイズ変更、属性、フォーカス、失敗時の復元、親消失、繰り返し終了、時計書式を確認します。

CIでExplorerの壁紙表示そのものは保証できません。Windowsの実画面での確認手順と対応範囲は [docs/VERIFICATION.md](docs/VERIFICATION.md) を参照してください。WorkerWは公開APIではなく、Explorerのバージョンによって利用できないことがあります。

`develop` が開発ブランチです。`main` への反映はPRで行います。構成は [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)、今後の計画は [docs/ROADMAP.md](docs/ROADMAP.md) を参照してください。
