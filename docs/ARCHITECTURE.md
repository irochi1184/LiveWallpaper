# Architecture

## 方針

LiveWallpaperは、Windows固有処理と画面・製品機能を分離します。

```text
LiveWallpaper.App
├─ WinUI 3画面
├─ WallpaperWindow
└─ 将来の設定画面・編集画面
       │
       ├── LiveWallpaper.Core
       │   ├─ 設定
       │   ├─ モデル
       │   └─ 将来の共通インターフェース
       │
       └── LiveWallpaper.Windows
           ├─ Win32 API
           ├─ WorkerW
           ├─ モニター制御
           └─ 全画面検出
```

## WorkerWについて

デスクトップのアイコンより後ろへ独自ウィンドウを置くため、ExplorerのWorkerWを利用する技術検証を行っています。

WorkerWへの配置はWindowsの正式な壁紙APIではなく、Explorer内部の挙動に依存します。そのため次を守ります。

1. Win32依存を `LiveWallpaper.Windows` に閉じ込める
2. Windows更新で挙動が変わった場合に交換できる設計にする
3. 失敗時に通常のアプリ画面まで巻き込まない
4. 対応Windows版を実機で継続検証する

## 時計

時計は壁紙そのものではなく、壁紙上に重ねる独立した部品として扱います。

初期実装では250msごとに現在時刻を確認し、表示文字列が変わった場合のみTextBlockを書き換えます。秒の進行をTimerで加算せず、OS時刻を毎回取得することでずれを防ぎます。

## 今後の描画層

```text
WallpaperWindow
├─ BackgroundRenderer
│   ├─ ImageRenderer
│   ├─ VideoRenderer
│   └─ WebRenderer
└─ WidgetLayer
    ├─ ClockWidget
    ├─ DateWidget
    ├─ SystemMonitorWidget
    └─ WeatherWidget
```

## 販売を見据えた境界

アプリ本体、壁紙形式、追加ウィジェット、販売・ライセンス機能は分離します。Microsoft StoreとSteamのどちらへ配布しても、描画中核を大きく変更しない構成を目指します。

## 時計壁紙のライフサイクル

MainWindowがWallpaperWindowを一つだけ所有する。WallpaperWindowは表示前にWorkerWWallpaperHostへ配置し、AppWindow.Show(false)で非アクティブ表示する。Activate()は時計に使わない。

WorkerWWallpaperHostはWS_POPUPを外してWS_CHILDを設定し、SetParentの戻り値と最終エラーを確認する。モニターのスクリーン座標をMapWindowPointsで親クライアント座標へ変換し、SetWindowPos(HWND_BOTTOM, SWP_NOACTIVATE)で配置する。Explorerのウィンドウ自体を隠したり破棄したりしない。通常のトップレベルWorkerWとProgman内のWorkerWの両方を探索する。

停止時はタイマーを停止し、時計を隠して元の親・属性へ戻した後にWinUI Window.Closeを呼ぶ。操作画面のClosedも同じ停止処理を通る。失敗時も配置をロールバックする。親の消失は毎秒検出し、手動での再表示を案内する。

参考: [SetParentの属性・DPI要件](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent)、[AppWindow.Show(Boolean)](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.show)。
