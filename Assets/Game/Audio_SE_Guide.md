# SE設定・管理

## 登録場所

Unityメニュー **Dead Shot > Audio > SE Management** で新規SEと現在開いているシーンの既存SEを編集できます。
**Create or Select SE Bank** で Assets/Game/Resources/DeadShotSoundBank.asset を選択できます。
新規音源は未登録です。nullは無音として扱います。音源をClipsにドラッグしてください。

| Sound | 再生条件 | 推奨素材 |
|---|---|---|
| CasingLand | 排莢イベントで生成した薬莢が床に最初に衝突した時。跳ね返りでは再生しない | 金属が床に落ちる短い1回音 |
| EmptyGun | 構え中、リロード中でなく、残弾0で射撃入力した時。弾・反動・スライドは動かさない | カチッという単発音 |
| PlayerImpact | 通常攻撃でPlayerのHPが減った時（這い攻撃も含む） | 打撃ダメージの単発音 |
| PlayerBite | EnemyAttackのmouthHitboxからHPが減った時 | 噛みつきダメージの単発音 |
| ZombieDeath | EnemyHealthでHPが0になった時、1回。死体消滅で音は切らない | 断末魔 |
| ZombieVoice | ゲーム操作可能中、カメラから音の範囲内で一定間隔にランダム再生 | 通常声2種類、各1回分 |
| BloodDrip | 生存ゾンビへの有効被弾時（致死弾も含む） | 血が滴る短い音 |

- Volume、Pitch、Spatial、Max Distanceを音ごとに調整できます。
- Voice Intervalは初期6〜12秒。Max Simultaneous Voicesは3体。同じゾンビの声は重複しません。
- Blood Drip Delayは初期0.15秒。既存の弾の命中音とは別に重ねます。
- 薬莢の音は実際の床との衝突が必要です。床Collider・薬莢Rigidbody/Collider・レイヤー衝突設定が必要で、着地前に薬莢が消える場合はSimpleShootのDestroy Timerを延ばしてください。
- 最終弾を撃った直後の既存自動リロードは維持しています。弾切れ入力そのものではリロードせずクリック音だけ鳴ります。リロード中は鳴りません。
- 通常声は新規バンクのランダム声です。既存EnemyAttackの索敵時AudioSourceは従来のまま別イベントとして残しています。同じ素材を登録して重なる場合は索敵音側のClipを外してください。

## 既存SEの整理

| 機能 | 主な既存設定元 |
|---|---|
| 銃声 | OnKeyPress_ThrowのAudioSource |
| ゾンビへの命中音、地面の命中音 | OnCollision_DestoryBothのenemyHitClips / groundImpactClips |
| プレイヤー・敵の足音 | FootstepAudioController（移動距離・歩行/走行別） |
| 構え・リロード・呼吸 | AudioManager |
| ライト点灯 | PlayerFlashlightControllerのturnOnSe |
| アイテム取得 | InventoryPickupのpickupSe |
| ポーズ・武器・アイテム選択 | PauseMenu、WeaponSelectionUI、InventoryUI |
| キー設定・カメラ設定 | 既存のポーズ／タイトルUI音の参照を利用 |
| OP地点選択 | LandingPointSelectionUI |
| 隊長会話・文字送り | CaptainHoverDialogue、TutorialUIManager2 |
| ニュース・ヘリ | OpeningNewsSequence、HelicopterController |
| タイトル・リザルト | TitleMenuController、ResultScoreDisplay（各シーンで確認） |

既存の音源登録先は破壊せず維持しました。一括管理ウィンドウは元のSerializedObjectをその場で編集するため、コピーとの食い違いはありません。別シーンはそのシーンを開く必要があります。配列の個数変更や詳細な設定は対象コンポーネントをInspectorで編集してください。

## 音量制御（後の音声設定画面向け）

DeadShotAudio.mixer にMaster、Effects、UI、Voice、Ambienceを用意しました。
既存の未ルーティングAudioSourceも実行中に収容します。すでに別Mixerが設定されている音源は上書きしません。
初期の自動分類は、ignoreListenerPause付き音源→UI、ニュース・隊長会話の配下→Voice、ループ音→Ambience、それ以外→Effectsです。
自動分類を変えたい音源には同じGameObjectに GameAudioRouting を追加してCategoryを指定してください。あるいはAudioSourceのOutputに直接Mixer Groupを登録します。
BGMは現在専用カテゴリを作らずAmbience側で管理できます。動画のDirect AudioはAudioSourceを通らないためこのMixerの対象外です。
各AudioSourceの元のvolume・pitch・OPのフェードは維持します。

設定画面から呼ぶ公開API（値は0〜1）:

    GameSoundService.SetMasterVolume(value);
    GameSoundService.SetCategoryVolume(GameAudioCategory.Effects, value);
    GameSoundService.SetCategoryVolume(GameAudioCategory.UI, value);
    GameSoundService.SetCategoryVolume(GameAudioCategory.Voice, value);
    GameSoundService.SetCategoryVolume(GameAudioCategory.Ambience, value);
    GameSoundService.SaveVolumes(); // 適用ボタンで保存

起動時に保存値を復元します。音声タブのスライダーUI自体は今回追加していません。

## 確認

Unityでコンパイル成功。Play Modeで新規7種の再生用AudioSource生成・Mixer参照と5個の公開音量パラメータを確認しました。
テストは実アセットを変更しない無音の一時クリップで行いました。実素材の聴感・薬莢の各地面での衝突・実戦のタイミングは、音源登録後に調整してください。
