# AirPurifier

あなたのお部屋の空気を綺麗にします。

- ゲーム中全体に漂う**ダストを除去**します。この機能はOFFにできません。
- その他パーティクル、デブリなど不要なものを削除できます。

---

## 導入方法

  `Plugins\AirPurifier.dll` を、Beat Saber のフォルダの `Plugins` に入れます。

## 設定

- メインメニューの Mod Settings の「Air Purifier」から設定します。初期値は全て OFF (0%) です。
- 変更した設定は、MOD Settings画面を \[Cancel\] ボタンで退出しても維持されます。

| 設定 | 内容 |
|---|---|
| Remove Debris | カットしたノーツの破片（デブリ）を消します|
| Remove Saber Clash Sparkles | セイバー同士が触れたときのパーティクルを消します|
| Remove Wall Sparkles | セイバーがウォールに触れたときのパーティクルと発光を消します |
| Hide Wall Inside | 壁の中身を消します。壁の枠のみが残ります|
| Skip Health Warning | 起動時の健康上の注意画面（Health and Safety）を飛ばします|

### ノーツカット時のパーティクル（全方向）

ノーツをカットしたときに四方へ飛び散るパーティクルを調整します。
Width/Length/Duration何れかを 0% にすることで完全に消すことができます。

| 設定 | 内容 |
|---|---|
| Change Note Cut Sparkles | ON で下の設定を表示します |
| Cut Sparkle Width | 太さ |
| Cut Sparkle Length | 長さ |
| Cut Sparkle Duration | 消えるまでの時間。長くするとその分遠くまで飛びます |
| Cut Sparkle 10x Limit | 同時に表示できる数の上限を 10 倍にします |
| Cut Sparkle Gravity | 弱い重力（5%）を付け、花火のように垂れ下がらせます |

### 斬撃方向のパーティクル

ノーツをカットしたときに、セイバーを振った方向へ伸びるパーティクルを調整します。
Width/Length/Duration何れかを 0% にすることで完全に消すことができます。

| 設定 | 内容 |
|---|---|
| Change Slash Effect | ON で下の設定を表示します |
| Slash Width | 太さ |
| Slash Length | 長さ |
| Slash Duration | 消えるまでの時間。長くするとその分遠くまで飛びます |
| Slash 10x Limit | 同時に表示できる数の上限を 10 倍にします |

### 補足

- Width/Length/Duration は 0〜5000%（10% 刻み）で、100% がゲーム本来の見た目です。
- Duration を長くすると同時に表示される数が増え、上限（500）に達すると以後のカットで表示されなくなります。その場合は 10x Limit を ON にしてください。数が増えるほど処理は重くなります。
- Cut Sparkle Gravity は、Cut Sparkle Duration を伸ばすと効果が分かりやすくなります。

