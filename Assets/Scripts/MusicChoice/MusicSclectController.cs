
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MusicInvaders.Data;

public class MusicSelectController : MonoBehaviour
{
    [Header("曲選択UI")]
    [Tooltip("上・中央・下の順番で3つ登録してください")]
    [SerializeField] private SongItem[] songItems;
    [SerializeField] private SongInfoPanel songInfoPanel;

    [Header("曲プレビュー")]
    [SerializeField] private AudioSource previewAudioSource;
    [SerializeField] private AudioClip[] songClips;

    [Header("曲移動ボタン")]
    [SerializeField] private Button upButton;
    [SerializeField] private Button downButton;

    [Header("難易度ボタン")]
    [SerializeField] private Button easyButton;
    [SerializeField] private Button normalButton;
    [SerializeField] private Button hardButton;

    [Header("スコア・ランク表示")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text bestScoreText;

    [Header("ランク画像")]
    [SerializeField] private Image rankImage;
    [SerializeField] private Sprite rankS;
    [SerializeField] private Sprite rankA;
    [SerializeField] private Sprite rankB;
    [SerializeField] private Sprite rankC;
    [SerializeField] private Sprite rankD;

    [Header("スコア計算用")]
    [SerializeField] private SongDatabaseSO songDatabase;

    [Header("共通のランク達成率設定")]
    [Tooltip("リザルト画面と同じScoreRankSettingsを割り当ててください")]
    [SerializeField] private ScoreRankSettings rankSettings;

    [Header("決定ボタン")]
    [SerializeField] private Button confirmButton;

    [Header("設定画面")]
    [Tooltip("OptionsPanelではなく、親のOptionsOverlayを登録してください")]
    [SerializeField] private GameObject settingsPanel;

    private readonly string[] songNames =
    {
        "ShiningStar",
        "MereFancy",
        "SnowDrive0123"
    };

    private readonly string[] artistNames =
    {
        "森田交一",
        "kさん",
        "sさん"
    };

    private readonly Sprite[] jacketSprites =
    {
        null,
        null,
        null
    };

    private readonly string[] difficultyNames =
    {
        "Easy",
        "Normal",
        "Hard"
    };

    // 各曲の難易度
    // 列：Easy, Normal, Hard
    private readonly int[,] difficultyStars =
    {
        { 1, 3, 4 },
        { 2, 3, 5 },
        { 1, 3, 5 }
    };

    // 中央に表示されている曲
    private int centerSongIndex = 0;

    // 初期難易度：Normal
    private int selectedDifficultyIndex = 1;

    private string currentSongName = string.Empty;
    private string currentArtistName = string.Empty;
    private string currentDifficultyName = string.Empty;

    public string CurrentSongName => currentSongName;
    public string CurrentDifficultyName => currentDifficultyName;

    private const int centerItemIndex = 1;

    private bool isSettingsOpen = false;

    private void Start()
    {
        RegisterButtonEvents();

        isSettingsOpen = false;

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        RestoreLastPlayedSelection();

        UpdateSelectedValues();
        UpdateSongList();
        UpdateRightPanel();
        UpdateDifficultySelection();
        UpdateScoreInfo();

        PlaySelectedMusic();
    }

    /// <summary>
    /// 直前にプレイした曲と難易度を復元する。
    /// </summary>
    private void RestoreLastPlayedSelection()
    {
        centerSongIndex = 0;
        selectedDifficultyIndex = 1;

        int songIndex = System.Array.IndexOf(
            songNames,
            GameSceneArgs.SelectedMusic
        );

        if (songIndex < 0)
        {
            return;
        }

        centerSongIndex = songIndex;

        int difficultyIndex = System.Array.IndexOf(
            difficultyNames,
            GameSceneArgs.SelectedDifficulty
        );

        if (difficultyIndex >= 0)
        {
            selectedDifficultyIndex = difficultyIndex;
        }
    }

    /// <summary>
    /// ボタンイベントを登録
    /// </summary>
    private void RegisterButtonEvents()
    {
        if (upButton != null)
        {
            upButton.onClick.AddListener(MoveUp);
        }
        else
        {
            Debug.LogWarning("Up Buttonが登録されていません。");
        }

        if (downButton != null)
        {
            downButton.onClick.AddListener(MoveDown);
        }
        else
        {
            Debug.LogWarning("Down Buttonが登録されていません。");
        }

        if (easyButton != null)
        {
            easyButton.onClick.AddListener(SelectEasy);
        }
        else
        {
            Debug.LogWarning("Easy Buttonが登録されていません。");
        }

        if (normalButton != null)
        {
            normalButton.onClick.AddListener(SelectNormal);
        }
        else
        {
            Debug.LogWarning("Normal Buttonが登録されていません。");
        }

        if (hardButton != null)
        {
            hardButton.onClick.AddListener(SelectHard);
        }
        else
        {
            Debug.LogWarning("Hard Buttonが登録されていません。");
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(StartSelectedGame);
        }
        else
        {
            Debug.LogWarning("Confirm Buttonが登録されていません。");
        }
    }

    /// <summary>
    /// ボタンイベントを解除
    /// </summary>
    private void OnDestroy()
    {
        if (upButton != null)
        {
            upButton.onClick.RemoveListener(MoveUp);
        }

        if (downButton != null)
        {
            downButton.onClick.RemoveListener(MoveDown);
        }

        if (easyButton != null)
        {
            easyButton.onClick.RemoveListener(SelectEasy);
        }

        if (normalButton != null)
        {
            normalButton.onClick.RemoveListener(SelectNormal);
        }

        if (hardButton != null)
        {
            hardButton.onClick.RemoveListener(SelectHard);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(StartSelectedGame);
        }
    }

    /// <summary>
    /// 上の三角ボタン
    /// </summary>
    public void MoveUp()
    {
        if (isSettingsOpen)
        {
            return;
        }

        centerSongIndex--;

        if (centerSongIndex < 0)
        {
            centerSongIndex = songNames.Length - 1;
        }

        UpdateMusicDisplay();

        Debug.Log("上へ移動：" + currentSongName);
    }

    /// <summary>
    /// 下の三角ボタン
    /// </summary>
    public void MoveDown()
    {
        if (isSettingsOpen)
        {
            return;
        }

        centerSongIndex++;

        if (centerSongIndex >= songNames.Length)
        {
            centerSongIndex = 0;
        }

        UpdateMusicDisplay();

        Debug.Log("下へ移動：" + currentSongName);
    }

    /// <summary>
    /// 曲に関係する表示をまとめて更新
    /// </summary>
    private void UpdateMusicDisplay()
    {
        UpdateSelectedValues();
        UpdateSongList();
        UpdateRightPanel();
        UpdateScoreInfo();
        PlaySelectedMusic();
    }

    public void SelectEasy()
    {
        SelectDifficulty(0);
    }

    public void SelectNormal()
    {
        SelectDifficulty(1);
    }

    public void SelectHard()
    {
        SelectDifficulty(2);
    }

    /// <summary>
    /// 難易度を変更
    /// </summary>
    private void SelectDifficulty(int difficultyIndex)
    {
        if (isSettingsOpen)
        {
            return;
        }

        if (difficultyIndex < 0 ||
            difficultyIndex >= difficultyNames.Length)
        {
            Debug.LogWarning(
                "存在しない難易度が指定されました。"
            );
            return;
        }

        selectedDifficultyIndex = difficultyIndex;

        UpdateSelectedValues();
        UpdateDifficultySelection();
        UpdateScoreInfo();

        Debug.Log(
            "選択：" +
            currentSongName +
            " / " +
            currentDifficultyName
        );
    }

    /// <summary>
    /// 決定ボタン
    /// </summary>
    public void StartSelectedGame()
    {
        if (isSettingsOpen)
        {
            return;
        }

        UpdateSelectedValues();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame(
                currentSongName,
                currentArtistName,
                currentDifficultyName
            );
        }
        else
        {
            Debug.LogWarning(
                "GameManagerが見つかりませんでした。"
            );
        }
    }

    /// <summary>
    /// 選択中の曲・作者・難易度を更新
    /// </summary>
    private void UpdateSelectedValues()
    {
        currentSongName =
            songNames[centerSongIndex];

        currentArtistName =
            artistNames[centerSongIndex];

        currentDifficultyName =
            difficultyNames[selectedDifficultyIndex];
    }

    /// <summary>
    /// 曲一覧を更新
    /// </summary>
    private void UpdateSongList()
    {
        if (songItems == null ||
            songItems.Length != 3)
        {
            Debug.LogWarning(
                "Song ItemsのSizeを3にしてください。"
            );
            return;
        }

        for (int i = 0; i < songItems.Length; i++)
        {
            if (songItems[i] == null)
            {
                continue;
            }

            int offset = i - centerItemIndex;
            int songIndex = centerSongIndex + offset;

            while (songIndex < 0)
            {
                songIndex += songNames.Length;
            }

            while (songIndex >= songNames.Length)
            {
                songIndex -= songNames.Length;
            }

            songItems[i].SetTitle(
                SongDisplayName.Get(songNames[songIndex])
            );

            songItems[i].SetSelected(
                i == centerItemIndex
            );
        }
    }

    /// <summary>
    /// 曲情報を更新
    /// </summary>
    private void UpdateRightPanel()
    {
        if (songInfoPanel == null)
        {
            return;
        }

        songInfoPanel.SetSongInfo(
            SongDisplayName.Get(songNames[centerSongIndex]),
            jacketSprites[centerSongIndex]
        );

        songInfoPanel.SetDifficultyStars(
            difficultyStars[centerSongIndex, 0],
            difficultyStars[centerSongIndex, 1],
            difficultyStars[centerSongIndex, 2]
        );
    }

    /// <summary>
    /// 難易度選択表示
    /// </summary>
    private void UpdateDifficultySelection()
    {
        if (songInfoPanel == null)
        {
            return;
        }

        songInfoPanel.SetDifficultySelected(
            selectedDifficultyIndex
        );
    }

    /// <summary>
    /// 選択中の曲・難易度の
    /// 最高スコアとランクを表示
    /// </summary>
    private void UpdateScoreInfo()
    {
        if ((rankText == null && rankImage == null) ||
            bestScoreText == null)
        {
            return;
        }

        // 保存されている最高スコア
        int highScore =
            HighScoreStorage.Get(
                currentSongName,
                currentDifficultyName
            );

        // その譜面の理論最大スコア
        int maxScore =
            new ScoreRankCalculator().CalculateMaxScore(
                songDatabase,
                currentSongName,
                currentDifficultyName
            );

        string rank = "-";

        // 一度でもスコアが保存されている場合
        if (highScore > 0)
        {
            rank =
                new ScoreRankCalculator().Calculate(
                    highScore,
                    maxScore,
                    rankSettings
                );
        }

        // 従来の文字表示
        if (rankText != null)
        {
            rankText.text = rank;
        }

        // 追加：ランク画像を更新
        UpdateRankImage(rank);

        // ベストスコア表示
        bestScoreText.text =
            highScore.ToString("N0");
    }

    /// <summary>
    /// ランクに応じて表示画像を切り替える
    /// </summary>
    private void UpdateRankImage(string rank)
    {
        if (rankImage == null)
        {
            return;
        }

        Sprite selectedSprite = null;

        switch (rank.Trim().ToUpperInvariant())
        {
            case "S":
                selectedSprite = rankS;
                break;

            case "A":
                selectedSprite = rankA;
                break;

            case "B":
                selectedSprite = rankB;
                break;

            case "C":
                selectedSprite = rankC;
                break;

            case "D":
                selectedSprite = rankD;
                break;

            default:
                selectedSprite = null;
                break;
        }

        rankImage.sprite = selectedSprite;

        // 未プレイの場合や画像未登録なら非表示
        rankImage.enabled = selectedSprite != null;
    }

    /// <summary>
    /// 設定画面を開く
    /// </summary>
    public void OpenSettings()
    {
        if (settingsPanel == null)
        {
            return;
        }

        isSettingsOpen = true;
        settingsPanel.SetActive(true);
    }

    /// <summary>
    /// 設定画面を閉じる
    /// </summary>
    public void CloseSettings()
    {
        isSettingsOpen = false;

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 選択曲をプレビュー再生
    /// </summary>
    private void PlaySelectedMusic()
    {
        if (previewAudioSource == null)
        {
            return;
        }

        if (songClips == null ||
            centerSongIndex < 0 ||
            centerSongIndex >= songClips.Length)
        {
            return;
        }

        AudioClip selectedClip =
            songClips[centerSongIndex];

        if (selectedClip == null)
        {
            previewAudioSource.Stop();
            return;
        }

        previewAudioSource.Stop();

        previewAudioSource.clip = selectedClip;
        previewAudioSource.time = 0f;
        previewAudioSource.Play();
    }
}
