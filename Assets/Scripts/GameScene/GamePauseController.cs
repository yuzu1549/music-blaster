using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// GameScene のポーズ状態と、ゲーム全体の停止・再開を管理する。
/// </summary>
public class GamePauseController : MonoBehaviour
{
    private enum PauseState
    {
        Playing,
        Paused,
        ResumeCountdown,
        Options,
    }

    private const int ResumeCountdownStartNumber = 3;
    private const float ResumeCountdownIntervalSeconds = 1f;
    private const string GameSceneName = "GameScene";
    private const string MusicSelectSceneName = "MusicSelectScene";

    [Header("ゲーム入力を読み取るクラス")]
    [SerializeField] private GameInputReader inputReader;

    [Header("楽曲とノーツを管理するクラス")]
    [SerializeField] private NoteManager noteManager;

    [Header("ポーズ画面のルートオブジェクト")]
    [SerializeField] private GameObject pauseOverlay;

    [Header("再開カウントダウンを表示するテキスト")]
    [SerializeField] private TMP_Text resumeCountdownText;

    [Header("オプション画面を制御するクラス")]
    [SerializeField] private OptionsOverlayController optionsOverlayController;

    [Header("キーコンフィグ画面を制御するクラス")]
    [SerializeField] private OptionsKeyConfigController
        optionsKeyConfigController;

    [Header("Esc でメニューを切り替えるときの効果音")]
    [SerializeField] private AudioClip pauseOpenClip;
    [SerializeField] private AudioMixerGroup pauseSeMixerGroup;
    [SerializeField, Range(0f, 1f)] private float pauseOpenVolumeScale = 0.4f;

    private PauseState currentState = PauseState.Playing;
    private Coroutine resumeCountdownCoroutine;
    private AudioSource pauseOpenSource;

    private void Awake()
    {
        pauseOpenSource = gameObject.AddComponent<AudioSource>();
        pauseOpenSource.playOnAwake = false;
        pauseOpenSource.outputAudioMixerGroup = pauseSeMixerGroup;
    }

    private void OnEnable()
    {
        if (optionsOverlayController != null)
        {
            optionsOverlayController.RhythmSettingsChanged +=
                HandleRhythmSettingsChanged;
        }
    }

    private void OnDisable()
    {
        if (optionsOverlayController != null)
        {
            optionsOverlayController.RhythmSettingsChanged -=
                HandleRhythmSettingsChanged;
        }
    }

    private void Start()
    {
        Time.timeScale = 1f;
        inputReader?.SetGameplayInputEnabled(true);
        SetPauseOverlayVisible(false);
        SetResumeCountdownVisible(false);
        optionsOverlayController?.CloseOptions();
    }

    /// <summary>
    /// Esc 入力でポーズ状態を切り替え、切り替わった場合に効果音を鳴らす。
    /// </summary>
    private void LateUpdate()
    {
        if (inputReader == null || !inputReader.WasPausePressed())
        {
            return;
        }

        PauseState stateBeforeInput = currentState;
        if (currentState == PauseState.Playing)
        {
            PauseGame();
        }
        else if (currentState == PauseState.Paused)
        {
            StartResumeCountdown();
        }
        else if (currentState == PauseState.ResumeCountdown)
        {
            CancelResumeCountdown();
        }
        else if (!WasKeyConfigCancelConsumed())
        {
            CloseOptions();
        }

        if (currentState != stateBeforeInput)
        {
            PlayPauseMenuSound();
        }
    }

    /// <summary>
    /// Esc によるメニューの切り替え時に、共通の効果音を再生する。
    /// </summary>
    private void PlayPauseMenuSound()
    {
        if (pauseOpenClip == null)
        {
            return;
        }

        pauseOpenSource.PlayOneShot(pauseOpenClip, pauseOpenVolumeScale);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            HandleFocusLost();
        }
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            HandleFocusLost();
        }
    }

    private void OnDestroy()
    {
        StopResumeCountdownCoroutine();
        Time.timeScale = 1f;
        inputReader?.SetGameplayInputEnabled(true);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.UnPauseSE();
        }
    }

    /// <summary>
    /// ゲーム進行、音声、ゲームプレイ入力を停止する。
    /// </summary>
    public void PauseGame()
    {
        if (currentState != PauseState.Playing || IsGameFinished())
        {
            return;
        }

        currentState = PauseState.Paused;
        StopResumeCountdownCoroutine();
        SetResumeCountdownVisible(false);
        SetPauseOverlayVisible(true);
        inputReader?.SetGameplayInputEnabled(false);

        if (noteManager != null)
        {
            noteManager.PauseMusic();
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PauseBGM();
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PauseSE();
        }

        Time.timeScale = 0f;
    }

    /// <summary>
    /// 3、2、1 のカウントダウンを開始する。
    /// </summary>
    public void StartResumeCountdown()
    {
        if (currentState != PauseState.Paused)
        {
            return;
        }

        currentState = PauseState.ResumeCountdown;
        SetPauseOverlayVisible(false);
        SetResumeCountdownVisible(true);
        StopResumeCountdownCoroutine();
        resumeCountdownCoroutine = StartCoroutine(ResumeCountdown());
    }

    /// <summary>
    /// 再開カウントダウンを中止してポーズ画面へ戻る。
    /// </summary>
    public void CancelResumeCountdown()
    {
        if (currentState != PauseState.ResumeCountdown)
        {
            return;
        }

        StopResumeCountdownCoroutine();
        SetResumeCountdownVisible(false);
        SetPauseOverlayVisible(true);
        currentState = PauseState.Paused;
    }

    /// <summary>
    /// ポーズ画面を隠してオプション画面を表示する。
    /// </summary>
    public void OpenOptions()
    {
        if (currentState != PauseState.Paused)
        {
            return;
        }

        if (optionsOverlayController == null)
        {
            Debug.LogWarning(
                "GamePauseController に OptionsOverlayController が"
                + "設定されていません。");
            return;
        }

        SetPauseOverlayVisible(false);
        optionsOverlayController.OpenOptions();
        currentState = PauseState.Options;
    }

    /// <summary>
    /// オプション画面を閉じてポーズ画面へ戻る。
    /// </summary>
    public void CloseOptions()
    {
        if (currentState != PauseState.Options)
        {
            return;
        }

        optionsKeyConfigController?.CancelKeySelection();
        optionsOverlayController?.CloseOptions();
        SetPauseOverlayVisible(true);
        currentState = PauseState.Paused;
    }

    /// <summary>
    /// 現在と同じ楽曲・難易度で GameScene を最初から開始する。
    /// </summary>
    public void RetryGame()
    {
        PrepareForSceneTransition();
        SceneManager.LoadScene(GameSceneName);
    }

    /// <summary>
    /// 現在のプレイを終了して選曲画面へ戻る。
    /// </summary>
    public void ReturnToMusicSelect()
    {
        PrepareForSceneTransition();
        SceneManager.LoadScene(MusicSelectSceneName);
    }

    /// <summary>
    /// フォーカス喪失時にプレイを停止する。
    /// </summary>
    private void HandleFocusLost()
    {
        if (currentState == PauseState.Playing)
        {
            PauseGame();
        }
        else if (currentState == PauseState.ResumeCountdown)
        {
            CancelResumeCountdown();
        }
    }

    /// <summary>
    /// ポーズ中に変更されたノーツ位置を即座に表示へ反映する。
    /// </summary>
    private void HandleRhythmSettingsChanged()
    {
        if (currentState != PauseState.Options || noteManager == null)
        {
            return;
        }

        noteManager.PreviewSettingsWhilePaused();
    }

    /// <summary>
    /// リアルタイムで再開カウントダウンを進める。
    /// </summary>
    private IEnumerator ResumeCountdown()
    {
        for (int number = ResumeCountdownStartNumber; 0 < number; number--)
        {
            if (resumeCountdownText != null)
            {
                resumeCountdownText.text = number.ToString();
            }

            yield return new WaitForSecondsRealtime(
                ResumeCountdownIntervalSeconds);

            if (currentState != PauseState.ResumeCountdown)
            {
                yield break;
            }
        }

        resumeCountdownCoroutine = null;
        SetResumeCountdownVisible(false);
        ResumeGame();
    }

    /// <summary>
    /// 停止しているゲーム進行、音声、ゲームプレイ入力を再開する。
    /// </summary>
    private void ResumeGame()
    {
        if (currentState != PauseState.ResumeCountdown)
        {
            return;
        }

        if (noteManager != null)
        {
            noteManager.ApplySettingsForResume();
            noteManager.UnpauseMusic();
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.UnPauseBGM();
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.UnPauseSE();
        }

        Time.timeScale = 1f;
        inputReader?.SetGameplayInputEnabled(true);
        currentState = PauseState.Playing;
    }

    /// <summary>
    /// 実行中の再開カウントダウンを停止する。
    /// </summary>
    private void StopResumeCountdownCoroutine()
    {
        if (resumeCountdownCoroutine == null)
        {
            return;
        }

        StopCoroutine(resumeCountdownCoroutine);
        resumeCountdownCoroutine = null;
    }

    /// <summary>
    /// シーン遷移前に時間、入力、音声の状態を通常状態へ戻す。
    /// </summary>
    private void PrepareForSceneTransition()
    {
        StopResumeCountdownCoroutine();
        optionsKeyConfigController?.CancelKeySelection();
        optionsOverlayController?.CloseOptions();
        SetPauseOverlayVisible(false);
        SetResumeCountdownVisible(false);

        if (noteManager != null)
        {
            noteManager.StopMusic();
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBGM();
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopSE();
        }

        Time.timeScale = 1f;
        inputReader?.SetGameplayInputEnabled(true);
        currentState = PauseState.Playing;
    }

    /// <summary>
    /// このフレームの Esc がキー変更キャンセルに使われたかを返す。
    /// </summary>
    /// <returns>キー変更キャンセルに使われた場合は true</returns>
    private bool WasKeyConfigCancelConsumed()
    {
        return optionsKeyConfigController != null
            && optionsKeyConfigController.WasCancelInputConsumedThisFrame;
    }

    /// <summary>
    /// ポーズ画面の表示状態を切り替える。
    /// </summary>
    /// <param name="isVisible">表示する場合は true</param>
    private void SetPauseOverlayVisible(bool isVisible)
    {
        if (pauseOverlay != null)
        {
            pauseOverlay.SetActive(isVisible);
        }
    }

    /// <summary>
    /// 再開カウントダウンの表示状態を切り替える。
    /// </summary>
    /// <param name="isVisible">表示する場合は true</param>
    private void SetResumeCountdownVisible(bool isVisible)
    {
        if (resumeCountdownText != null)
        {
            resumeCountdownText.gameObject.SetActive(isVisible);
        }
    }

    /// <summary>
    /// ゲームクリアまたはゲームオーバーが確定しているかを返す。
    /// </summary>
    /// <returns>ゲーム終了済みの場合は true</returns>
    private bool IsGameFinished()
    {
        return GameManager.Instance != null
            && (GameManager.Instance.isGameOver
                || GameManager.Instance.isGameCleared);
    }

    public void CloseOptionsAndResume()
    {
        Debug.Log("設定画面を閉じて再開します");

        CloseOptions();

        Debug.Log("CloseOptions実行後: " + currentState);

        StartResumeCountdown();

        Debug.Log("StartResumeCountdown実行後: " + currentState);
    }
}
