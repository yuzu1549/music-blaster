using UnityEngine;
using System.Collections.Generic;
using MusicInvaders.Data;

public class NoteManager : MonoBehaviour
{
	[Header("参照")]
	public TextAsset chartText;
	public AudioSource audioSource;
	public ObjectPool pool;
	public SongDatabaseSO songDatabase;

	[Header("表示上の判定線")]
	public Transform judgementLineTransform;

	[Header("ノーツをSpriteMaskの内側だけ表示するか")]
	[SerializeField] private bool useNoteVisibilityMask = false;

	[Header("設定")]
	public float bpm = 158.0f;
	public float baseDuration = 2.0f;
	public float noteSpeed = 1.0f;
	public float delayTime = 3.0f;    // 全体の開始待ち時間

	[Header("■■■ デバッグ・テスト専用 ■■■")]
	[Tooltip("ONにすると下の小節から曲と譜面を途中再生します")]
	public bool isDebugMode = false;

	[Tooltip("何小節目からスタートするか（1で曲の最初から）")]
	public int startMeasure = 1;

	[Tooltip("1小節を何拍とするか（一般的な曲は4拍子なので4）")]
	public int beatsPerMeasure = 4;

	private List<NoteData> notes = new List<NoteData>();
	private double dspStartTime;
	private bool isMusicScheduled = false;
	private float songOffsetSeconds = 0f;
	private int enemyStartBeat = 0;
	private float timingOffsetSeconds = 0f;
	private bool hasMusicStarted = false;
	private bool isPaused = false;
	private bool wasMusicPlayingBeforePause = false;
	private double pauseStartedDspTime;
	private float pausedAudioTime;
	private bool useEnemyEndBeat;
	private float enemyEndBeat;
	private float initialMusicVolume;
	private bool hasEndingController;

	public bool IsPlaybackPaused => isPaused;
	public bool IsTimelineRunning => isMusicScheduled && !isPaused &&
		AudioSettings.dspTime - dspStartTime >= delayTime;
	public bool HasReachedEnding => isMusicScheduled && !isPaused &&
		(useEnemyEndBeat
			? GetCurrentMusicTime() >= GetTargetTime(enemyEndBeat, SecondsPerBeat)
			: hasMusicStarted && !audioSource.isPlaying);

	/// <summary>終了進行を専用コントローラーへ委譲する。</summary>
	public void RegisterEndingController()
	{
		hasEndingController = true;
	}

	/// <summary>設定音量とは独立した楽曲のフェード倍率を反映する。</summary>
	public void SetEndingFade(float multiplier)
	{
		if (audioSource != null)
		{
			audioSource.volume = initialMusicVolume * Mathf.Clamp01(multiplier);
		}
	}

	public float CurrentMusicTimeSeconds => GetCurrentMusicTime();
	public float SecondsPerBeat => bpm > 0f ? 60f / bpm : 0f;
	public int BeatsPerMeasure => beatsPerMeasure;
	public float EnemyBeatStartTimeSeconds =>
		songOffsetSeconds - timingOffsetSeconds +
		enemyStartBeat * SecondsPerBeat;
	public int TimingOffsetRevision { get; private set; }
	public bool IsMusicPlaying =>
		audioSource != null && audioSource.isPlaying && !isPaused;

	// 途中から始めた分の時間を記憶する変数
	private float debugStartTimeOffset = 0f;

	struct NoteData
	{
		public float beat;
		public int lane;
	}

	void Start()
	{
		// 前のシーンから曲データと難易度が渡されていればセットアップする
		SetupFromArgs();
		ApplySettings();
		initialMusicVolume = audioSource != null ? audioSource.volume : 1f;

		// 譜面データを読み込む
		LoadChart();

		float secondsPerBeat = 60.0f / bpm;
		float actualDuration = GetNoteVisibleDurationSeconds();

		// デバッグモード：テスト用の途中再生処理
		if (isDebugMode && startMeasure > 1)
		{
			// 開始する拍数 = (指定した小節 - 1) × 1小節の拍数
			float startBeat = (startMeasure - 1) * beatsPerMeasure;
			debugStartTimeOffset = startBeat * secondsPerBeat;

			if (audioSource != null && audioSource.clip != null)
			{
				// 曲の再生位置（秒）を、指定した小節まで一気に進める
				audioSource.time = Mathf.Min(debugStartTimeOffset, audioSource.clip.length);
			}

			// 既に通り過ぎているべき過去のノーツをリストから削除しておく
			notes.RemoveAll(n => GetTargetTime(n.beat, secondsPerBeat) < debugStartTimeOffset - actualDuration);

			// コンソールに黄色い警告を出して、テスト中であることを知らせる
			Debug.LogWarning($"【テストモード作動中】第{startMeasure}小節（{debugStartTimeOffset:F2}秒）から再生します！");
		}

		dspStartTime = AudioSettings.dspTime;

		if (audioSource != null && audioSource.clip != null)
		{
			// 再生終了を正常に検出するため、曲をループしない設定にする
			audioSource.loop = false;
			// 精密な時間で再生を予約する
			audioSource.PlayScheduled(dspStartTime + delayTime);
			isMusicScheduled = true;
			hasMusicStarted = false;
		}
		else
		{
			Debug.LogError("AudioSourceまたはClipが設定されていません！");
		}
	}

	/// <summary>
	/// GameSettings に保存されているリズム設定を反映する。
	/// </summary>
	private void ApplySettings()
	{
		noteSpeed = GameSettings.NoteSpeed;

		float updatedTimingOffsetSeconds =
			GameSettings.TimingOffsetMs / 1000f;
		if (!Mathf.Approximately(
			timingOffsetSeconds,
			updatedTimingOffsetSeconds))
		{
			timingOffsetSeconds = updatedTimingOffsetSeconds;
			TimingOffsetRevision++;
		}
	}

	/// <summary>
	/// ポーズ中に変更されたリズム設定を未判定ノーツへ反映する。
	/// </summary>
	public void ApplySettingsForResume()
	{
		float previousTimingOffsetSeconds = timingOffsetSeconds;
		ApplySettings();

		float noteVisibleDuration = GetNoteVisibleDurationSeconds();
		float timingOffsetDeltaSeconds =
			timingOffsetSeconds - previousTimingOffsetSeconds;
		bool shouldResolveMisses = hasMusicStarted;

		ApplySettingsToActiveNotes(
			noteVisibleDuration,
			timingOffsetDeltaSeconds,
			shouldResolveMisses);
		SpawnOrMissPendingNotes(
			noteVisibleDuration,
			shouldResolveMisses);
	}

	/// <summary>
	/// ポーズ中の設定変更を画面上のノーツへ即座に反映する。
	/// </summary>
	public void PreviewSettingsWhilePaused()
	{
		if (!isPaused)
		{
			return;
		}

		float previousTimingOffsetSeconds = timingOffsetSeconds;
		ApplySettings();

		float noteVisibleDuration = GetNoteVisibleDurationSeconds();
		float timingOffsetDeltaSeconds =
			timingOffsetSeconds - previousTimingOffsetSeconds;

		ApplySettingsToActiveNotes(
			noteVisibleDuration,
			timingOffsetDeltaSeconds,
			false);
		SpawnOrMissPendingNotes(noteVisibleDuration, false);
	}

	/// <summary>
	/// ノーツが出現してから判定ラインに到達するまでの時間を返す。
	/// </summary>
	/// <returns>ノーツ表示時間（秒）</returns>
	private float GetNoteVisibleDurationSeconds()
	{
		return 5.0f / noteSpeed;
	}

	// シーン間のデータ引き継ぎ処理
	// Resourcesではなく、SongDatabaseから検索してロードする
	// 選択された曲と譜面をデータベースから取得する
	void SetupFromArgs()
	{
		string targetSongName = GameSceneArgs.SelectedMusic;
		string targetDifficulty = GameSceneArgs.SelectedDifficulty;

		if (string.IsNullOrEmpty(targetSongName) || string.IsNullOrEmpty(targetDifficulty))
		{
			Debug.LogWarning("データが渡されていません。インスペクターの初期設定を使用します。");
			return;
		}

		if (songDatabase == null)
		{
			Debug.LogError("❌ SongDatabaseSO がインスペクターにセットされていません！");
			return;
		}

		// 1. データベースから曲名で検索
		MusicInvaders.Data.SongData foundSong = songDatabase.FindSong(targetSongName);

		if (foundSong != null)
		{
			// 曲をセット
			audioSource.clip = foundSong.bgm;

			// 🔥【追加】データベースのBPMで、NoteManagerのBPMを上書きする！
			bpm = foundSong.bpm;
			songOffsetSeconds = foundSong.SongOffsetSeconds;
			enemyStartBeat = foundSong.EnemyStartBeat;
			useEnemyEndBeat = foundSong.UseEnemyEndBeat;
			enemyEndBeat = foundSong.EnemyEndBeat;
			if (useEnemyEndBeat && (float.IsNaN(enemyEndBeat) ||
				float.IsInfinity(enemyEndBeat) || enemyEndBeat <= enemyStartBeat))
			{
				Debug.LogError("敵の終了拍は開始拍より後に設定してください。音源終了を使用します。");
				useEnemyEndBeat = false;
			}

			Debug.Log($"🎵 BGMをセットしました: {targetSongName} (BPM: {bpm})");

			// 2. その曲の中から、難易度が一致する譜面を検索
			MusicInvaders.Data.ChartData foundChart = foundSong.charts.Find(c => c.difficultyName == targetDifficulty);

			if (foundChart != null)
			{
				// 譜面テキストをセット
				chartText = foundChart.chartFile;
				Debug.Log($"✨ 譜面をセットしました: {targetSongName} - {targetDifficulty}");

				if (GameManager.Instance != null)
				{
					GameManager.Instance.maxScore =
						new ScoreRankCalculator().CalculateMaxScore(
							songDatabase,
							GameManager.Instance.musicTitle,
							GameManager.Instance.difficulty
						);
				}
			}
			else
			{
				Debug.LogError($"❌ 難易度【{targetDifficulty}】の譜面が登録されていません！");
			}
		}
		else
		{
			Debug.LogError($"❌ 曲名【{targetSongName}】がデータベースに登録されていません！");
		}
	}

	void LoadChart()
	{
		// chartTextが空ならエラーを防ぐために処理しない
		if (chartText == null) return;

		string[] lines = chartText.text.Split('\n');
		foreach (string line in lines)
		{
			if (string.IsNullOrWhiteSpace(line)) continue;

			string[] values = line.Split(',');
			if (values.Length == 2)
			{
				notes.Add(new NoteData
				{
					beat = float.Parse(values[0]),
					lane = int.Parse(values[1])
				});
			}
		}
	}

	void Update()
	{
		if (audioSource == null || !isMusicScheduled || isPaused) return;

		double currentDspTime = AudioSettings.dspTime - dspStartTime;
		float currentMusicTime;

		// 待ち時間を過ぎて曲が鳴り始めているはずの時はオーディオの再生位置（秒）を基準にする
		if (currentDspTime >= delayTime)
		{
			currentMusicTime = GetCurrentMusicTime();

			if (!hasMusicStarted && audioSource.isPlaying)
			{
				hasMusicStarted = true;
			}
		}
		else
		{
			// まだ曲が鳴る前の待ち時間（delayTime中）は、時計の計算でノーツを先出しする
			currentMusicTime = (float)(currentDspTime - delayTime) + debugStartTimeOffset;
		}

		if (!hasEndingController && hasMusicStarted && !audioSource.isPlaying)
		{
			// 再生開始後に停止している場合、曲の再生が終わったと判断してクリアを実行
			OnMusicEnded();
			return;
		}

		float actualDuration = GetNoteVisibleDurationSeconds();
		float secondsPerBeat = 60.0f / bpm;

		for (int i = 0; i < notes.Count; i++)
		{
			float targetTime = GetTargetTime(notes[i].beat, secondsPerBeat);

			// 判定ラインに到達する時間 - ノーツが移動する時間
			if (currentMusicTime >= targetTime - actualDuration)
			{
				SpawnNote(notes[i].lane, actualDuration);
				notes.RemoveAt(i);
				i--;
			}
		}
	}

	private void OnMusicEnded()
	{
		if (GameManager.Instance == null) return;
		if (GameManager.Instance.isGameOver || GameManager.Instance.isGameCleared) return;

		GameFinish gameFinish = FindFirstObjectByType<GameFinish>();
		if (gameFinish != null)
		{
			gameFinish.GameClear();
		}
		else
		{
			Debug.LogWarning("GameFinish が見つかりません。曲終了時の GameClear を実行できませんでした。");
		}
	}

	void SpawnNote(int lane, float actualDuration)
	{
		SpawnNote(lane, actualDuration, 0f);
	}

	void SpawnNote(int lane, float actualDuration, float elapsedTime)
	{
		GameObject obj = pool.Get();
		if (obj != null)
		{
			Pseudo3DNote noteScript = obj.GetComponent<Pseudo3DNote>();
			if (noteScript != null)
			{
				noteScript.SetVisibilityMaskEnabled(
					useNoteVisibilityMask);

				float judgementLineY = GetJudgementLineLocalY(obj.transform, noteScript.endY);
				noteScript.Init(
					lane,
					actualDuration,
					judgementLineY,
					elapsedTime);
			}
		}
	}

	/// <summary>
	/// 画面上に存在する未判定ノーツへ変更後の設定を反映する。
	/// </summary>
	private void ApplySettingsToActiveNotes(
		float noteVisibleDuration,
		float timingOffsetDeltaSeconds,
		bool shouldResolveMisses)
	{
		if (pool == null || pool.container == null)
		{
			return;
		}

		Pseudo3DNote[] activeNotes =
			pool.container.GetComponentsInChildren<Pseudo3DNote>(false);

		foreach (Pseudo3DNote activeNote in activeNotes)
		{
			float judgementLineY = GetJudgementLineLocalY(
				activeNote.transform,
				activeNote.endY);

			activeNote.ApplyPlaybackSettings(
				noteVisibleDuration,
				timingOffsetDeltaSeconds,
				judgementLineY,
				shouldResolveMisses);
		}
	}

	/// <summary>
	/// 設定変更後の時刻を基準に、未生成ノーツの生成または MISS を行う。
	/// </summary>
	/// <param name="noteVisibleDuration">変更後のノーツ表示時間</param>
	/// <param name="shouldResolveMisses">
	/// 強制 MISS 時刻を過ぎたノーツを確定する場合は true
	/// </param>
	private void SpawnOrMissPendingNotes(
		float noteVisibleDuration,
		bool shouldResolveMisses)
	{
		float currentMusicTime = GetCurrentMusicTime();
		float secondsPerBeat = 60.0f / bpm;
		float missWindow = (JudgementManager.Instance != null)
			? JudgementManager.Instance.missWindow
			: 0.1666f;

		for (int i = 0; i < notes.Count; i++)
		{
			float targetTime = GetTargetTime(notes[i].beat, secondsPerBeat);
			float timeDiff = currentMusicTime - targetTime;

			if (shouldResolveMisses && missWindow < timeDiff)
			{
				JudgementManager.Instance?.DisplayMiss();
				notes.RemoveAt(i);
				i--;
				continue;
			}

			if (-noteVisibleDuration <= timeDiff)
			{
				float elapsedTime = noteVisibleDuration + timeDiff;

				SpawnNote(notes[i].lane, noteVisibleDuration, elapsedTime);
				notes.RemoveAt(i);
				i--;
			}
		}
	}

	/// <summary>
	/// ポーズ状態を考慮した現在の楽曲時間を返す。
	/// </summary>
	/// <returns>現在の楽曲時間（秒）</returns>
	private float GetCurrentMusicTime()
	{
		if (audioSource == null)
		{
			return 0f;
		}

		if (isPaused)
		{
			return pausedAudioTime;
		}

		double currentDspTime = AudioSettings.dspTime - dspStartTime;

		// Web では AudioSource.time がポーズ解除後に先へ進むことがある。
		// 譜面と拍は、ポーズ時間を差し引いた DSP 時刻で揃える。
		if (Application.platform != RuntimePlatform.WebGLPlayer &&
			delayTime <= currentDspTime && audioSource.isPlaying)
		{
			return audioSource.time;
		}

		return (float)(currentDspTime - delayTime)
			+ debugStartTimeOffset;
	}

	/// <summary>
	/// ノーツが判定位置に到達する曲中時間を返す。
	/// </summary>
	/// <param name="beat">譜面上の拍</param>
	/// <param name="secondsPerBeat">1拍あたりの秒数</param>
	/// <returns>判定位置に到達する曲中時間</returns>
	private float GetTargetTime(float beat, float secondsPerBeat)
	{
		return (beat * secondsPerBeat)
			+ songOffsetSeconds
			- timingOffsetSeconds;
	}

	/// <summary>
	/// 表示上の判定線を、ノーツの親を基準にした Y 座標へ変換する。
	/// </summary>
	/// <param name="noteTransform">生成されたノーツの Transform</param>
	/// <param name="fallbackY">判定線が未設定の場合に使う Y 座標</param>
	/// <returns>ノーツのローカル座標系における判定線の Y 座標</returns>
	private float GetJudgementLineLocalY(Transform noteTransform, float fallbackY)
	{
		if (judgementLineTransform == null)
		{
			return fallbackY;
		}

		if (noteTransform.parent == null)
		{
			return judgementLineTransform.position.y;
		}

		Vector3 localPosition = noteTransform.parent.InverseTransformPoint(judgementLineTransform.position);
		return localPosition.y;
	}

	/// <summary>
	/// 音楽の再生を停止します（外部から呼び出す用）。
	/// </summary>
	public void StopMusic()
	{
		if (audioSource == null) return;

		// 再生予約・再生を停止し、フラグをリセット
		audioSource.Stop();
		isMusicScheduled = false;
		isPaused = false;
		wasMusicPlayingBeforePause = false;
	}

	/// <summary>
	/// 音楽と楽曲時間の進行を一時停止する。
	/// </summary>
	public void PauseMusic()
	{
		if (audioSource == null || isPaused) return;

		pausedAudioTime = GetCurrentMusicTime();
		isPaused = true;
		pauseStartedDspTime = AudioSettings.dspTime;
		double elapsedDspTime = pauseStartedDspTime - dspStartTime;
		bool hasReachedScheduledStart = delayTime <= elapsedDspTime;
		wasMusicPlayingBeforePause =
			hasReachedScheduledStart && audioSource.isPlaying;

		if (wasMusicPlayingBeforePause)
		{
			hasMusicStarted = true;
		}

		if (wasMusicPlayingBeforePause)
		{
			if (Application.platform == RuntimePlatform.WebGLPlayer)
			{
				// Web の Pause/UnPause は再開位置がずれる場合がある。
				audioSource.Stop();
			}
			else
			{
				audioSource.Pause();
			}
			return;
		}

		// 開始待機中の予約再生は、再開時に残り待機時間を保って組み直す。
		if (isMusicScheduled)
		{
			audioSource.Stop();
		}
	}

	/// <summary>
	/// 音楽と楽曲時間の進行を再開する。
	/// </summary>
	public void UnpauseMusic()
	{
		if (audioSource == null || !isPaused) return;

		double resumeDspTime = AudioSettings.dspTime;
		double pauseDuration = resumeDspTime - pauseStartedDspTime;
		dspStartTime += pauseDuration;

		if (wasMusicPlayingBeforePause)
		{
			if (Application.platform == RuntimePlatform.WebGLPlayer)
			{
				audioSource.time = Mathf.Clamp(
					pausedAudioTime, 0f, audioSource.clip.length);
				audioSource.Play();
			}
			else
			{
				audioSource.UnPause();
			}
		}
		else if (isMusicScheduled && !hasMusicStarted && audioSource.clip != null)
		{
			audioSource.time = Mathf.Min(debugStartTimeOffset, audioSource.clip.length);
			audioSource.PlayScheduled(dspStartTime + delayTime);
		}

		isPaused = false;
		wasMusicPlayingBeforePause = false;
	}
}
