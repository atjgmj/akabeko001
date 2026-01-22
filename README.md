# 赤べこCG首振りアプリ

日本の伝統的な郷土玩具「赤べこ」をモチーフにした3DCG首振りトイアプリ。

## 🎮 機能

### Phase 1 (MVP)
- ✅ リアルな首振り物理演算（バネ-ダンパーモデル）
- ✅ スワイプ入力検出
- ✅ スクリーンショット機能
- ✅ シェア機能
- ✅ データ保存（累計スワイプ回数、発見済みモーション）

### Phase 2 (レアモーション)
- ✅ データ駆動型レアモーションシステム
- ✅ 色変更モーション（金・銀・虹）
- ✅ 背景変更モーション（桜・宇宙・雪）
- ⏳ 発動演出（SE・パーティクル）

## 🛠️ 開発環境

- **Unity**: 6.3 LTS (6000.3.2f1)
- **ターゲットプラットフォーム**: WebGL（優先）、Steam、iOS、Android
- **3Dモデル**: Blender 3.3.9で作成済み

## 📁 プロジェクト構造

```
Assets/
├── Akabeko/
│   ├── Models/              # 3Dモデル（FBX）
│   ├── Textures/            # テクスチャ
│   ├── Materials/           # マテリアル
│   ├── Scripts/             # C#スクリプト
│   ├── Scenes/              # シーン
│   ├── Audio/SE/            # 効果音
│   ├── Backgrounds/         # 背景画像
│   └── Resources/
│       └── Data/RareMotions/ # レアモーションJSON定義
```

## 🚀 セットアップ手順

### 1. Unityプロジェクトを開く
```bash
# Unity Hub から Unity 6.3 LTS でプロジェクトを開く
```

### 2. 必要なパッケージをインストール
- TextMeshPro（UI用）
- Unity Ads（広告用、将来実装）

### 3. シーンを作成
1. `Assets/Akabeko/Scenes/MainScene.unity` を作成
2. 以下のGameObjectを配置：
   - Main Camera（CameraControllerアタッチ）
   - Akabeko（モデル + AkabekoController, SwipeDetector, NeckPhysics）
   - Canvas（UI）
   - DataManager
   - UIManager
   - ScreenshotManager
   - ShareManager

### 4. レイヤー設定
- 新しいレイヤー「Neck」を作成
- 赤べこの首部分に「Neck」レイヤーを設定

## 📝 スクリプト一覧

| スクリプト | 役割 |
|-----------|------|
| `AkabekoController.cs` | メインコントローラー |
| `SwipeDetector.cs` | スワイプ入力検出 |
| `NeckPhysics.cs` | 首の物理演算 |
| `RareMotionSystem.cs` | レアモーション判定 |
| `DataManager.cs` | データ保存・読み込み |
| `UIManager.cs` | UI制御 |
| `ScreenshotManager.cs` | スクリーンショット |
| `ShareManager.cs` | シェア機能 |
| `CameraController.cs` | カメラ設定 |

## 🎨 レアモーションの追加方法

1. `Assets/Akabeko/Resources/Data/RareMotions/` に新しいJSONファイルを作成
2. 以下の形式で定義：

```json
{
  "motionId": "unique_id",
  "motionName": "表示名",
  "type": 0,
  "conditions": [
    {
      "type": 0,
      "parameters": {
        "rate": "0.05"
      }
    }
  ],
  "rarity": 1,
  "materialName": "Mat_Name",
  "backgroundName": "",
  "animationName": ""
}
```

### 条件タイプ
- `0`: PROB（確率）
- `1`: TAP_PART（部位タップ）
- `2`: SWIPE_PATTERN（パターン）
- `3`: SWIPE_SPEED（速度）
- `4`: COUNT（累計回数）
- `5`: TIME（時間帯）
- `6`: DATE（日付）

### モーションタイプ
- `0`: COLOR_CHANGE（色変更）
- `1`: BACKGROUND_CHANGE（背景変更）
- `2`: ANIMATION（アニメーション）

### レア度
- `0`: COMMON（★）
- `1`: RARE（★★）
- `2`: SUPER_RARE（★★★）
- `3`: ULTRA_RARE（★★★★）

## 🎯 次のステップ

### Unity エディタでの作業
1. メインシーンの作成
2. 赤べこモデルの配置とセットアップ
3. UIキャンバスの作成
4. マテリアルの作成（通常、金、銀、虹）
5. 背景画像の配置

### テスト
1. エディタでの動作確認
2. WebGLビルド
3. 実機テスト

## 📄 ライセンス

このプロジェクトは個人開発プロジェクトです。

## 📞 お問い合わせ

質問や提案があれば、Issueを作成してください。
