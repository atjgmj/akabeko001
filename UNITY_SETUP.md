# Unity プロジェクトセットアップガイド

このドキュメントは、赤べこアプリをUnityで開くための手順を説明します。

## 前提条件

- Unity Hub がインストールされていること
- Unity 6.3 LTS (6000.3.2f1) がインストールされていること

## セットアップ手順

### 1. Unity Hub でプロジェクトを開く

1. Unity Hub を起動
2. 「プロジェクト」タブで「開く」をクリック
3. このフォルダ（`akabeko`）を選択
4. Unity 6.3 LTS でプロジェクトを開く

### 2. TextMeshPro のセットアップ

Unity 6 では、TextMeshPro パッケージは **デフォルトで含まれています**。
ただし、使用するには **TMP Essential Resources** を手動でインポートする必要があります。

#### TMP Essential Resources のインポート（必須）
1. Unity エディタを開く
2. メニュー: `Window` > `TextMeshPro` > `Import TMP Essential Resources` を選択
3. インポートウィンドウが開くので、`Import` をクリック
4. `Assets/TextMesh Pro/Resources/` フォルダが作成されます

> [!TIP]
> **メニューが表示されない場合**
> 1. `Window` > `Package Manager` を開く
> 2. `Packages: Unity Registry` を選択
> 3. 「TextMeshPro」を検索し、`Install` または `Update` をクリックしてください。
> その後、改めて上記の `Import TMP Essential Resources` を実行します。

#### TMP Examples & Extras のインポート（任意、推奨）
1. メニュー: `Window` > `TextMeshPro` > `Import TMP Examples & Extras`
2. 例とサンプルが `Assets/TextMesh Pro/Examples & Extras/` にインポートされます
3. 初めて TextMeshPro を使う場合は、学習のためにインポートを推奨します

### 3. プロジェクト設定

#### レイヤーの追加
1. メニュー: `Edit` > `Project Settings` > `Tags and Layers`
2. 「Layers」セクションで空いているレイヤーに「Neck」を追加

#### ビルド設定（Unity 6 の Build Profiles）

Unity 6 では **Build Profiles** という新しいビルドシステムが導入されています。

##### Web プロファイルをアクティブに設定
1. メニュー: `File` > `Build Profiles`
2. **Web** プロファイルを選択
3. **「Set Active」** ボタンをクリック（または右クリックして「Set as Active」）
4. これで WebGL がアクティブなビルドターゲットになります

> [!NOTE]
> **Unity 6 での Web サポート**
> Unity 6 からは、WebGL（Web）ビルドがモバイルブラウザ（iOS Safari/Android Chrome）で公式にサポートされています。
> これにより、以前のバージョンよりもパフォーマンスが向上し、スマホでの動作確認も容易になっています。

> [!TIP]
> **開発中のヒント**
> - Windows プロファイルも残しておくと、開発中の高速テストに便利です
> - プロファイルを切り替えるだけで、異なるプラットフォーム向けにビルドできます
> - ビルド時は、対象のプロファイルをアクティブにしてから `Build` を実行します

##### Player Settings の設定
1. メニュー: `File` > `Build Profiles` を開く
2. ウィンドウ左下にある **「Player Settings...」ボタン** をクリック
   （または、`Edit` > `Project Settings` > `Player` からも開けます）
3. 以下を設定：
   - **Company Name**: あなたの名前
   - **Product Name**: Akabeko
   - **Default Icon**: （任意）

##### 将来的なプラットフォーム追加
要件定義では以下のプラットフォームも予定されています：
- **Steam（Windows/Mac）**: Windows プロファイルをそのまま使用可能
- **iOS**: 新しく iOS プロファイルを追加
- **Android**: 新しく Android プロファイルを追加

### 4. メインシーンの作成

1. `Assets/Akabeko/Scenes/` フォルダを右クリック
2. `Create` > `Scene` を選択
3. 「MainScene」と名前を付ける
4. ダブルクリックでシーンを開く

### 5. シーンのセットアップ

#### GameObjectの配置と構成

Hierarchyウィンドウで、以下の構成になるように作成・配置します。
「既存」は新規シーン作成時に最初からあるもの、「新規」は右クリックメニューから作成するものです。

| GameObject名 | 状態 | 形式 | 作成方法 / 備考 |
| :--- | :--- | :--- | :--- |
| **Main Camera** | 既存 | Camera | 最初から存在します |
| **Directional Light** | 既存 | Light | 最初から存在します |
| **Akabeko** | **新規** | 空のオブジェクト | `Create Empty` で作成 |
| └── **Model** | **新規** | モデル (FBX) | `akabeko.fbx` を Akabeko の子にドラッグ |
| **Canvas** | **新規** | UI Canvas | `UI` > `Canvas` で作成 |
| ├── **ScreenshotButton** | **新規** | UI Button (TMP) | `UI` > `Button - TextMeshPro` |
| ├── **ShareButton** | **新規** | UI Button (TMP) | `UI` > `Button - TextMeshPro` |
| └── **RareMotionPanel** | **新規** | UI Panel | `UI` > `Panel` |
| **EventSystem** | **自動作成** | UI EventSystem | Canvas作成時に自動で作成されます |
| **Managers** | **新規** | 空のオブジェクト | `Create Empty` で作成 |
| ├── **DataManager** | **新規** | 空のオブジェクト | Managers の子として作成 |
| ├── **UIManager** | **新規** | 空のオブジェクト | Managers の子として作成 |
| ├── **ScreenshotManager** | **新規** | 空のオブジェクト | Managers の子として作成 |
| └── **ShareManager** | **新規** | 空のオブジェクト | Managers の子として作成 |

#### コンポーネントのアタッチ方法

スクリプトや機能をGameObjectに追加するには以下の手順で行います：

1. **Hierarchy** で対象の GameObject を選択
2. **Inspector** ウィンドウの最下部にある **「Add Component」** ボタンをクリック
3. 検索ボックスにスクリプト名（例：`AkabekoController`）を入力して選択
4. または、`Assets` フォルダ内のスクリプトファイルを、対象の GameObject（Hierarchy / Inspector）へ直接 **ドラッグ＆ドロップ** することでも追加可能です。

#### コンポーネントのアタッチ一覧

##### Main Camera
- `CameraController.cs` をアタッチ
- Target Object に Akabeko の Neck を設定

##### Akabeko GameObject
- `AkabekoController.cs` をアタッチ
  - **Neck Transform**: Hierarchy の **Neck** オブジェクトをドラッグ＆ドロップ
  - **Akabeko Renderer**: Hierarchy の **Model**（または Body/Neck の Renderer）をドラッグ＆ドロップ
- `SwipeDetector.cs` をアタッチ
  - **Neck Layer**: ドロップダウンをクリックし、「**Neck**」のみにチェックを入れる
- `NeckPhysics.cs` をアタッチ
  - **Neck Transform**: Hierarchy の **Neck** オブジェクトをドラッグ＆ドロップ
- `RareMotionSystem.cs` をアタッチ

##### Managers
- 各マネージャーに対応するスクリプトをアタッチ

### 6. モデルのセットアップ

4. FBXをシーンにドラッグ&ドロップ
5. **リネームによる整理**:
   - `立方体` -> **`Body`** に変更
   - `立方体.001` -> **`Neck`** に変更
6. **レイヤーの設定**:
   - `Neck` オブジェクトを選択し、Layerを「**Neck**」に設定
7. **コライダー（当たり判定）の追加** (重要):
   - `Neck` オブジェクトを選択
   - Inspector の `Add Component` から **`Box Collider`** を追加
   - 首全体を覆うように、必要に応じて `Edit Collider` ボタンでサイズを調整

### 7. マテリアルの作成

1. `Assets/Akabeko/Materials/` フォルダで右クリック
2. `Create` > `Material` を選択
3. 以下のマテリアルを作成：
   - **通常時 (Default)**: `Mat_Default_body`, `Mat_Default_neck`
   - **レア時 (Gold)**: `Mat_Gold_body`, `Mat_Gold_neck`
   - その他（必要に応じて）: `Mat_Silver`, `Mat_Rainbow`

4. **テクスチャの設定手順**:
   1. `Project` ウィンドウで作成したマテリアル（例：`Mat_Default`）を選択
   2. **Inspector** ウィンドウの **「Surface Inputs」** セクションにある **「Base Map」** (または Albedo) の横の小さな四角い枠を確認
   3. `Assets/Akabeko/Textures/` などにあるテクスチャファイルを、その四角い枠へ **ドラッグ＆ドロップ** します
   4. 必要に応じて、Base Map の横のカラーピッカーでベースの色を調整します（金色なら黄色系にするなど）

5. **モデルへの適用**:
   - 作成したマテリアルを、Hierarchy 上の赤べこモデル（Model）へ直接ドラッグ＆ドロップして適用します

### 8. UIの作成

#### Canvas設定
1. Hierarchy で右クリック > `UI` > `Canvas`
2. Canvas Scaler を「Scale With Screen Size」に設定
3. Reference Resolution: 1920 x 1080

#### ボタンの作成
1. Canvas を右クリック > `UI` > `Button - TextMeshPro`
2. 「ScreenshotButton」と「ShareButton」を作成
3. 右上に配置

### 9. 動作確認

1. Play ボタンを押してゲームを実行
2. 赤べこをスワイプして首が揺れることを確認
3. スクリーンショットボタンをテスト
4. **WebGL ビルドのテスト**:
   - `Build` 実行後、ブラウザで動作を確認してください
   - **スマホでの確認**: Unity 6 はモバイル Web を公式サポートしているため、実機のスマホ（iPhone/Android）でもスワイプ操作や表示に問題がないか確認推奨です

## トラブルシューティング

### スクリプトのコンパイルエラー
- Unity エディタを再起動してください
- `Assets` > `Reimport All` を実行

### モデルが表示されない
- FBXのインポート設定を確認
- マテリアルが正しく設定されているか確認

### スワイプが反応しない / 首が動かない
- **Inspector の未設定**: `AkabekoController` や `NeckPhysics` の **Neck Transform** が空（None）になっていないか確認してください。
- **レイヤー設定**: 首に「Neck」レイヤーが設定されているか確認してください。
- **レイヤーマスク**: SwipeDetector の Layer Mask が「Neck」になっているか確認してください。
- **コライダー**: 首に Box Collider が付いており、サイズが適切か確認してください。

### 赤べこが半透明に見える
- **Material の設定**: `Assets/Akabeko/Materials/` のマテリアルを選択し、Inspector で以下を確認してください。
  - **Surface Type** (URP) または **Rendering Mode**: 「**Opaque**」になっているか確認してください（Transparent になっていると半透明になります）。
  - **Alpha**: Base Map のカラー設定で Alpha が 255 (最大) になっているか確認してください。
- **Lighting**: シーンが明るすぎる場合、マテリアルが飛んで半透明に見えることがあります。Directional Light の Intensity を下げてみてください。

## 次のステップ

セットアップが完了したら、以下を試してください：

1. レアモーションのテスト
2. WebGLビルド
3. カスタマイズ（色、背景など）

詳細は `README.md` を参照してください。
