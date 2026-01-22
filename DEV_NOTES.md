# 赤べこアプリ 開発メモ

## 実装済み機能

### Phase 1: MVP
- [x] プロジェクト構造作成
- [x] 全スクリプト実装
  - AkabekoController.cs
  - SwipeDetector.cs
  - NeckPhysics.cs
  - RareMotionSystem.cs
  - DataManager.cs
  - UIManager.cs
  - ScreenshotManager.cs
  - ShareManager.cs
  - CameraController.cs
- [x] レアモーションJSONサンプル作成
- [x] ドキュメント作成

### 未実装（Unity エディタでの作業が必要）
- [ ] メインシーンの作成
- [ ] モデルの配置とセットアップ
- [ ] UIキャンバスの作成
- [ ] マテリアルの作成
- [ ] 背景画像の作成
- [ ] 効果音の追加

## 次のステップ

1. Unity Hub でプロジェクトを開く
2. UNITY_SETUP.md の手順に従ってセットアップ
3. メインシーンを作成
4. 動作確認

## 注意事項

### モデルの階層構造
赤べこモデルは以下の構造が必要：
```
Akabeko
├── Body（胴体）
└── Neck（首+頭）
```

現在のFBXファイルの構造を確認し、必要に応じてBlenderで調整が必要。

### レアモーションのテスト
デバッグモードで確率を100%にしてテストすることを推奨：
```json
{
  "type": 0,
  "parameters": {
    "rate": "1.0"
  }
}
```

### WebGLビルド時の注意
- PlayerPrefs は WebGL でも動作するが、ブラウザのローカルストレージを使用
- スクリーンショットは自動ダウンロードされる
- シェア機能はTwitterリンクを開く

## 将来の拡張

### Phase 2
- [ ] アニメーションモーション（宇宙に飛ぶ、回転、ジャンプ）
- [ ] コレクション画面
- [ ] 効果音・BGM

### Phase 3
- [ ] 首分離モデル
- [ ] Shape Keys（変形）
- [ ] 追加のカラバリ・背景

### Phase 4
- [ ] マルチプレイヤー機能？
- [ ] カスタマイズ機能
- [ ] ガチャシステム？
