# Roslynを用いたUnity開発のためのツール開発

## 概要

Unityプロジェクトにおける堅牢なアーキテクチャ設計 (DDDなど) を支援するための, Roslynベースの静的解析・可視化ツール群です.


## セットアップ

1. 本リポジトリをクローンする
2. ターミナルでリポジトリのルートディレクトリを開き, `dotnet build` を実行しビルドする.
3. `/Hoge/bin/Debug/netstandard2.0` の中に `Hoge.dll` が生成される
4. 使用したいツールの `.dll` ファイルをUnityプロジェクトの `Assets` 配下に配置する
5. Unityエディタ上で `.dll` を選択し, Inspectorから以下の設定を行う
   - `Include Platforms` のチェックをすべて外す
   - `Validate References` のチェックをすべて外す
   - ウィンドウ下部の `Asset Labels` をクリックし, `RoslynAnalyzer` と入力して適用 (Apply) する


## 動作要件

- Unity2020.2 以上


## 開発環境

- .NET SDK 10.x 以上
- `dotnet` CLI (ビルド・パッケージ管理用 / SDKに標準同梱)
- macOS (VSCode + C# Dev Kit にて動作確認済)


## 各ツールの詳細

> T.B.W.