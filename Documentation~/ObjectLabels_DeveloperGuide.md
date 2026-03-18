# Object Labels - 開発者ガイド

Object Labels のラベルシステムを利用してエディタ拡張やツールを作成する開発者向けのガイドです。

---

## 対応バージョン

- Unity 2022.3 LTS 以降

## 導入方法

### UPM (Git URL)

Unity Package Manager で以下の git URL を指定してください:

```
https://github.com/mugisennin/object-labels.git
```

### VCC (VRChat Creator Companion)

VCC の Settings > Packages > 「Add Repository」で以下の URL を追加してください:

```
https://mugisennin.github.io/vpm-listing/index.json
```

---

## パッケージ構成

```
com.mugisennin.object-labels/
├── package.json
├── Runtime/
│   ├── Mugisennin.ObjectLabels.asmdef
│   └── ObjectLabels.cs              # MonoBehaviour（ランタイム・エディタ共通）
├── Editor/
│   ├── Mugisennin.ObjectLabels.Editor.asmdef
│   ├── LabelSettings.cs              # スロット名管理（エディタ専用）
│   ├── ObjectLabelsEditor.cs         # インスペクター拡張
│   ├── LabelManagerWindow.cs         # ラベル定義管理ウィンドウ（Window > Label Manager）
│   ├── LabelVisibilityWindow.cs      # 表示/非表示切り替えウィンドウ（Window > Label Visibility）
│   └── LabelActiveWindow.cs          # アクティブ/非アクティブ切り替えウィンドウ（Window > Label Active）
└── Documentation~/
    └── ObjectLabels_DeveloperGuide.md
```

### 設計上の前提

- ラベルは **スロット番号（int: 0〜999）** で管理される
- スロット番号と名前のマッピングは `ProjectSettings/LabelSettings.json` に保存（プロジェクト側）
- `ObjectLabels` コンポーネントはスロット番号の配列のみを保持する
- ラベル名の解決はエディタ側（`LabelSettings`）が担当する

### Assembly Definition の参照

本パッケージの API を利用するスクリプトでは、asmdef の参照が必要です:

- **ランタイムスクリプト** → `Mugisennin.ObjectLabels` を参照
- **エディタスクリプト** → `Mugisennin.ObjectLabels.Editor` を参照（`LabelSettings` を使う場合）

asmdef を使用していないスクリプト（`Assets/` 直下など）からは、参照設定なしで `ObjectLabels` を利用できます。

---

## ObjectLabels コンポーネント API

`ObjectLabels` は `MonoBehaviour` であり、パッケージの `Runtime/` に配置されています。
エディタスクリプト・ランタイムスクリプトの両方から参照できます。

### プロパティ

| メンバー | 型 | 説明 |
|---|---|---|
| `LabelSlots` | `IReadOnlyList<int>` | 付与されているスロット番号の読み取り専用リスト |

### メソッド

| メソッド | 説明 |
|---|---|
| `HasLabel(int slot)` | 指定スロットが付与されていれば `true` |
| `AddLabel(int slot)` | スロットを追加（重複時は無視） |
| `RemoveLabel(int slot)` | スロットを削除 |
| `ClearLabels()` | 全スロットを削除 |

### 制約

- `[DisallowMultipleComponent]` — 1つの GameObject に1つだけ配置可能
- 内部データは `List<int>` でシリアライズされる

---

## LabelSettings 静的クラス API

`LabelSettings` はパッケージの `Editor/` に配置されたエディタ専用クラスです。
**`#if UNITY_EDITOR` ブロック内、またはEditorフォルダ内のスクリプトからのみ使用してください。**

### 定数

| 定数 | 値 | 説明 |
|---|---|---|
| `MaxSlots` | `1000` | スロットの最大数（0〜999） |

### よく使うメソッド

| メソッド | 説明 |
|---|---|
| `EnsureLoaded()` | 未読み込みなら JSON を読み込む。毎フレーム呼んでも軽量 |
| `GetName(int slot)` | スロット番号 → ラベル名。未定義なら `null` |
| `GetDefinedSlots()` | 定義済みスロットを `List<KeyValuePair<int, string>>` で返す（番号順） |
| `FindOrphanedSlots()` | シーン内で使用されているが名前未定義のスロット番号リスト |

### 書き込み系メソッド

| メソッド | 説明 |
|---|---|
| `SetName(int slot, string name)` | スロットに名前を設定。`name` が空なら削除 |
| `RemoveName(int slot)` | スロットの名前を削除 |
| `Save()` | 変更を `ProjectSettings/LabelSettings.json` に書き出す |
| `Reload()` | JSON から強制再読み込み |

### インポート/エクスポート

| メソッド | 説明 |
|---|---|
| `ExportToFile(string path)` | 現在の定義を JSON ファイルに書き出す |
| `ParseImportFile(string path)` | JSON ファイルを読み込み `Dictionary<int, string>` で返す（適用はしない） |

---

## 実装パターン集

### パターン1: 特定ラベルを持つオブジェクトを全取得

```csharp
// エディタスクリプトでの基本パターン
var allLabels = FindObjectsOfType<ObjectLabels>();
var enemies = allLabels
    .Where(ol => ol.HasLabel(slotIndex))
    .Select(ol => ol.gameObject)
    .ToList();
```

### パターン2: ラベル名からスロット番号を逆引き

```csharp
LabelSettings.EnsureLoaded();
int? slot = LabelSettings.GetDefinedSlots()
    .Where(kvp => kvp.Value == "Enemy")
    .Select(kvp => (int?)kvp.Key)
    .FirstOrDefault();

if (slot.HasValue)
{
    // slot.Value を使ってオブジェクトを検索
}
```

### パターン3: 非アクティブオブジェクトも含めて検索

```csharp
// 非アクティブの GameObject は通常の FindObjectsOfType では見つからない
// includeInactive: true を指定する
var allLabels = FindObjectsOfType<ObjectLabels>(true);
```

### パターン4: EditorWindow の基本テンプレート

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class MyLabelToolWindow : EditorWindow
{
    private Vector2 _scrollPos;

    [MenuItem("Window/My Label Tool")]
    public static void ShowWindow()
    {
        GetWindow<MyLabelToolWindow>("My Label Tool");
    }

    private void OnFocus()
    {
        LabelSettings.EnsureLoaded();
    }

    private void OnGUI()
    {
        LabelSettings.EnsureLoaded();
        var defined = LabelSettings.GetDefinedSlots();
        var allLabels = FindObjectsOfType<ObjectLabels>();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        foreach (var kvp in defined)
        {
            var objects = allLabels
                .Where(ol => ol.HasLabel(kvp.Key))
                .Select(ol => ol.gameObject)
                .ToList();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"[{kvp.Key}] {kvp.Value} ({objects.Count})");

            // ここにカスタム操作を追加
            if (GUILayout.Button("Do Something", GUILayout.Width(100)))
            {
                foreach (var go in objects)
                {
                    // 処理
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }
}
```

### パターン5: Undo 対応の変更

```csharp
// GameObject の状態を変更するときは Undo を記録する
foreach (var go in objects)
{
    Undo.RecordObject(go, "My Operation");
    go.SetActive(false);
}
```

### パターン6: SceneVisibilityManager でエディタ上の表示制御

```csharp
var svm = SceneVisibilityManager.instance;

// 非表示にする（第2引数: 子オブジェクトにも適用するか）
svm.Hide(gameObject, false);

// 表示する
svm.Show(gameObject, false);

// 現在非表示かどうか
bool isHidden = svm.IsHidden(gameObject);
```

### パターン7: 操作後にエディタUIを即座に反映する

```csharp
// SceneVisibilityManager で表示状態を変更した後、
// ヒエラルキーやウィンドウの表示を即座に更新するには
// EditorApplication.delayCall を使って次フレームで再描画する
EditorApplication.RepaintHierarchyWindow();
EditorApplication.delayCall += () =>
{
    // 状態を再取得してから再描画
    Repaint();
};
```

---

## 注意事項

### LabelSettings はエディタ専用
`LabelSettings` はパッケージの `Editor/` に配置されているため、ランタイムビルドには含まれません。ランタイムでラベル名を使いたい場合は、別途スロット番号を定数として管理してください。

```csharp
// ランタイムではスロット番号を直接使う
public static class LabelSlotConstants
{
    public const int Enemy = 0;
    public const int Interactable = 3;
}

// 使用例
if (GetComponent<ObjectLabels>().HasLabel(LabelSlotConstants.Enemy))
{
    // ...
}
```

### FindObjectsOfType のコスト
`FindObjectsOfType` はシーン内の全オブジェクトを走査します。`OnGUI` 内で毎フレーム呼ぶのはエディタ用途では許容範囲ですが、大量のオブジェクトがあるシーンでは結果をキャッシュすることを検討してください。

### スロット番号の安定性
スロット番号は Prefab やシーンファイルにシリアライズされます。一度運用を始めたスロット番号を別の意味に使い回さないでください。不要になったラベルは名前を削除しても、そのスロット番号は欠番として残すことを推奨します。

### ラベル定義の共有
- `ProjectSettings/LabelSettings.json`（スロット名マッピング）はプロジェクト固有のファイルであり、パッケージには含まれません
- プロジェクト間でラベル定義を共有するには **Label Manager の Export/Import 機能** を使用してください
- アセットのインポートがラベル定義より先に行われた場合、コンポーネント上では一時的に `(undefined)` と表示されますが、ラベル定義をインポートすれば解消されます
- Label Manager ウィンドウでは、未定義スロットがシーン内で使用されている場合に警告が表示されます

### .meta ファイルについて
パッケージのリリース zip には必ず `.meta` ファイルを含めてください。Unity はコンポーネントの参照を `.meta` 内の GUID で管理するため、`.meta` ファイルが欠けるとバージョンアップ時に Script Missing が発生します。本パッケージでは GitHub Actions ワークフローで自動的に `.meta` ファイル込みの zip が生成されます。
