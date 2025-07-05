# AI向け資料
## プロジェクト概要
VRChat使用目的で販売されているアバターを表示・操作するEditor拡張

## 特記事項
VRChat向けのPackage Manager(vpm)での配布を目的としている
そのため、開発のルートは Assets/ 下でなく、Packages/com.naninunenoy.avatar-viewer/ 下となる

## 実装方針
### 可読性重視
このクラスの何を変えるとどう動作が変わるかを自信をもって変更できる状態を保つのが第一とする。
行数は短いことが望ましいが、利用クラスが多い場合は長くなる場合も許容。
async/awaitを使用し、実装が上から下に流れるように読める状態を保つこと。
コールバックなどのコードジャンプは避ける。
クラスのメンバ変数の数は最小限に抑える。また可能な限りreadonlyにする。
Array/List/Collectionが必須でない場合はIEnumerableを公開あるいは入力とする。
外部に公開する際は可能な限りIEnumerable/IReadOnly/ReadOnlyとして公開する。
可能な限り(著しく可読性を下げない限り)無駄なAllocやGCを避けること。
すべてのクラス・メソッド・メンバ変数にはドキュメントコメントをつけること。
しかし、classの継承やinterfaceの実装時には不要。
Linqは積極的に使ってください。ただし中の条件式が複雑になりすぎないよう注意してください。
Linqではクエリ構文は使わずメソッド構文を使用すること。

### クラス分離
クラスの相互参照は許さない。
データクラスを積極的に作り、可能な限りイミュータブルにする。
データの識別にはintやstringをそのまま使わずID用の構造体やenumを定義する。
Unityにおいてenumは変更に弱いので特にその値がファイルに保存される場合は使用しないこと。
依存の注入は可能な限りコンストラクタで行う。次点で init プロパティ。
リソースの破棄をIDisposableで可能か限り行うこと。
publicは変数は使用せずプロパティを使用すること。
public readonly変数は使用せずgetのみのプロパティを使用すること。auto-propertyで実装すること。
クラスの実装は以下の順番で行うこと
* const定数
* static readonly変数
* staticコンストラクタ
* public staticメソッド
* readonly変数
* publicプロパティ
* コンストラクタ
* (あるばあいは)IDisposable.Disposeの実装
* publicメソッド
* privateメソッド
* private staticメソッド
非同期処理にはAsyncサフィックスを付けること。
非同期処理の最後の引数にはCancellationTokenを渡すこと。

### 依存整理
interfaceを使うのは実装が分岐する場合もしくはUnity外の実装詳細を隠ぺいする場合。
委譲や拡張でのDRYを推奨。継承は継承によりメリットが大きい場合のみ使用する。

## コードスタイル
基本は ./.editorconfig を参照のこと。
不要なusingは削除すること。
private 修飾子は不要。
floatの初期化では X.0F を使用すること。
varか型名明記はどちらでもいいが、型名が長いと思われる場合はvarを使って。
80文字を目安に改行。ただし少しくらいなら超えてよいので無理な改行はせず読みやすさを優先。
メンバ変数にはtarget typed newを使用。
ローカル変数にはtarget typed newを使わない。
classやstructの初期化はdefaultを使用。
stringの初期化はstring.Emptyを使用。
