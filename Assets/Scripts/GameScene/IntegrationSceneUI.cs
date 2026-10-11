using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Globalization;

/// IntegrationScenes上に曲情報・時間・オプション・スコア情報を表示する。
public class IntegrationSceneUI : MonoBehaviour
{
    [Header("Left UI Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI artistText;
    [SerializeField] private TextMeshProUGUI difficultyText;
    [SerializeField] private TextMeshProUGUI highScoreText;
    //[SerializeField] private TextMeshProUGUI timeText;
    //[SerializeField] private TextMeshProUGUI optionTitleText;
    //[SerializeField] private TextMeshProUGUI notesSpeedText;
    //[SerializeField] private TextMeshProUGUI timingOffsetText;

    [Header("Right UI Text")]
    [SerializeField] private TextMeshProUGUI lifeText;
    [Header("Life Images")]
    [SerializeField] private Sprite lifeSprite;
    [SerializeField, Min(1f)] private float lifeIconSize = 40f;
    [SerializeField, Min(0.1f)] private float lifeIconScale = 1.4f;
    [SerializeField, Min(0f)] private float lifeIconSpacing = 8f;
    private Image[] lifeImages;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI perfectCountText;
    [SerializeField] private TextMeshProUGUI goodCountText;
    [SerializeField] private TextMeshProUGUI missCountText;

    [Header("Game Over UI")]
    [SerializeField] private TextMeshProUGUI gameOverText;

    //[Header("Music Clip")]
    //[SerializeField] private AudioClip musicClip;

    [Header("Audio Source")]
    [SerializeField] private AudioSource musicAudioSource;

    [Header("Test Timer")]
    [SerializeField] private bool startTimerOnAwake = true;

    [Header("Player Health")]
    [SerializeField] private PlayerHealth playerHealth;

    private float currentTime = 0f;
    private bool isTimerRunning = false;
    private bool isGameOver = false;

    private void Start()
    {
        // 前回のゲームオーバーなどで止まったままにならないようにする
        Time.timeScale = 1f;

        if (startTimerOnAwake)
        {
            isTimerRunning = true;
        }

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }

        ConfigureScoreText();
        ConfigureJudgementCount(perfectCountText);
        ConfigureJudgementCount(goodCountText);
        ConfigureJudgementCount(missCountText);

        UpdateAllTexts();
        UpdateScoreTexts();

        GameManager.Instance.OnGameStatsChanged += UpdateScoreTexts;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStatsChanged -= UpdateScoreTexts;
        }
    }

    private void Update()
    {
        if (isGameOver)
        {
            return;
        }

        if (isTimerRunning)
        {
            currentTime += Time.deltaTime;

            //if (musicClip != null && currentTime > musicClip.length)
            //{
                //currentTime = musicClip.length;
                //isTimerRunning = false;
            //
            //}
        }

        UpdateAllTexts();
    }

    private void UpdateAllTexts()
    {
        UpdateMusicTexts();
        UpdateOptionTexts();
        UpdateLifeImages();
    }

    private void UpdateMusicTexts()
    {
        //string currentTimeText = FormatTime(currentTime);

        //if (musicClip != null)
        //{
            //songTitle = musicClip.name;
            //totalTimeText = FormatTime(musicClip.length);
        //}

        UpdateMusicInfoText(titleText, "Title", GameManager.Instance.musicTitle);
        UpdateMusicInfoText(artistText, "Artist", GameManager.Instance.artistName);
        UpdateMusicInfoText(difficultyText, "Difficulty", GameManager.Instance.difficulty,
            GetDifficultyColor(GameManager.Instance.difficulty));

        if (highScoreText != null)
        {
            int highScore = HighScoreStorage.Get(
                GameManager.Instance.musicTitle,
                GameManager.Instance.difficulty
            );

            highScoreText.text = $"最高スコア：{highScore}";
            highScoreText.fontSize = 32;
        }

        //[if (timeText != null)
        //{
            //timeText.text = $"Time:\n{currentTimeText} / {totalTimeText}";
            //timeText.fontSize = 32;
        //}
    }

    private static void UpdateMusicInfoText(TextMeshProUGUI text, string label, string value, string valueColor = null)
    {
        if (text == null) return;

        string content = string.IsNullOrEmpty(value) ? "Unknown" : value;
        if (!string.IsNullOrEmpty(valueColor))
        {
            content = $"<color=#{valueColor}>{content}</color>";
        }
        text.text = $"<size=75%><color=#B0B8C4>{label}</color></size>\n{content}";
        text.fontSize = 40;
    }

    private static string GetDifficultyColor(string difficulty)
    {
        switch (difficulty)
        {
            case "Easy": return "87CEFA";
            case "Normal": return "7FE39A";
            case "Hard":
            case "Difficult": return "FFA45B";
            default: return "FFFFFF";
        }
    }

    private void UpdateOptionTexts()
    {
        // if (optionTitleText != null)
        // {
        //     optionTitleText.text = "Option";
        //     optionTitleText.fontSize = 28;
        // }

        // if (notesSpeedText != null)
        // {
        //     notesSpeedText.text = $"Notes Speed: {GameSettings.NoteSpeed:F1}";
        //     notesSpeedText.fontSize = 24;
        // }

        // if (timingOffsetText != null)
        // {
        //     timingOffsetText.text = $"Timing Offset: {GameSettings.TimingOffsetMs} ms";
        //     timingOffsetText.fontSize = 24;
        // }
    }

    private void UpdateScoreTexts()
    {

        UpdateScoreText(GameManager.Instance.score);

        UpdateJudgementCount(perfectCountText, "P", "FFFF00", GameManager.Instance.perfectCount);
        UpdateJudgementCount(goodCountText, "G", "87CEFA", GameManager.Instance.goodCount);
        UpdateJudgementCount(missCountText, "M", "C0C0C0", GameManager.Instance.missCount);
    }

    private void ConfigureScoreText()
    {
        if (scoreText == null) return;

        const float rowWidth = 290f;
        RectTransform rect = scoreText.rectTransform;
        float left = rect.anchoredPosition.x - rect.rect.width * rect.pivot.x;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rowWidth);
        rect.anchoredPosition = new Vector2(left + rowWidth * rect.pivot.x, rect.anchoredPosition.y);
        scoreText.margin = Vector4.zero;
        scoreText.textWrappingMode = TextWrappingModes.NoWrap;
        scoreText.alignment = TextAlignmentOptions.TopLeft;
        scoreText.richText = true;
        scoreText.fontSize = 40;
        // 数字だけを縮めるため、サイズ調整はUpdateScoreTextで行う。
        scoreText.enableAutoSizing = false;
        scoreText.fontSizeMin = 20;
        scoreText.fontSizeMax = 40;
    }

    private void UpdateScoreText(int score)
    {
        if (scoreText == null) return;

        const string label = "<size=75%>Score:</size>";
        string number = score.ToString(CultureInfo.InvariantCulture);
        float rowWidth = scoreText.rectTransform.rect.width;
        float labelWidth = scoreText.GetPreferredValues(label, Mathf.Infinity, Mathf.Infinity).x;
        float numberWidth = scoreText.GetPreferredValues(number, Mathf.Infinity, Mathf.Infinity).x;
        float availableWidth = Mathf.Max(1f, rowWidth - labelWidth - 12f);
        float numberSize = Mathf.Clamp(40f * availableWidth / Mathf.Max(1f, numberWidth), 20f, 40f);
        string size = numberSize.ToString("0.###", CultureInfo.InvariantCulture);
        string sizedNumber = $"<size={size}>{number}</size>";
        float sizedWidth = scoreText.GetPreferredValues(sizedNumber, Mathf.Infinity, Mathf.Infinity).x;
        string position = (rowWidth - sizedWidth).ToString("0.###", CultureInfo.InvariantCulture);
        scoreText.text = $"{label}<pos={position}>{sizedNumber}";
    }

    private void ConfigureJudgementCount(TextMeshProUGUI text)
    {
        if (text == null) return;

        const float rowWidth = 290f;
        RectTransform rect = text.rectTransform;
        RectTransform reference = scoreText != null ? scoreText.rectTransform : rect;
        float left = reference.anchoredPosition.x - reference.rect.width * reference.pivot.x;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rowWidth);
        rect.anchoredPosition = new Vector2(left + rowWidth * rect.pivot.x, rect.anchoredPosition.y);
        text.margin = Vector4.zero;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.richText = true;
        text.color = Color.white;
        text.fontSize = 40;
    }

    private static void UpdateJudgementCount(TextMeshProUGUI text, string label, string color, int count)
    {
        if (text == null) return;

        string number = count.ToString(CultureInfo.InvariantCulture);
        float numberWidth = text.GetPreferredValues(number, Mathf.Infinity, Mathf.Infinity).x;
        float numberPosition = Mathf.Max(60f, text.rectTransform.rect.width - numberWidth);
        string position = numberPosition.ToString("0.###", CultureInfo.InvariantCulture);
        // 同じ行にラベルと数字を描画し、数字の幅に合わせて右端を揃える。
        text.text = $"<color=#{color}>{label}:</color><pos={position}>{number}";
    }

    private void UpdateLifeImages()
    {
        if (lifeText == null || lifeSprite == null) return;

        int maxLife = playerHealth != null ? Mathf.Max(0, playerHealth.maxHealth) : 3;
        int life = playerHealth != null ? Mathf.Clamp(playerHealth.currentHealth, 0, maxLife) : maxLife;

        if (lifeImages == null || lifeImages.Length != maxLife)
        {
            if (lifeImages != null)
            {
                foreach (Image icon in lifeImages)
                {
                    icon.gameObject.SetActive(false);
                    Destroy(icon.gameObject);
                }
            }

            // 既存のライフ表示位置を画像の親として利用する。
            lifeText.text = string.Empty;
            lifeText.enabled = false;
            lifeImages = new Image[maxLife];
            for (int i = 0; i < maxLife; i++)
            {
                GameObject iconObject = new GameObject("LifeIcon" + (i + 1), typeof(RectTransform), typeof(Image));
                iconObject.layer = lifeText.gameObject.layer;
                Image icon = iconObject.GetComponent<Image>();
                icon.rectTransform.SetParent(lifeText.rectTransform, false);
                icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                lifeImages[i] = icon;
            }
        }

        float iconSize = lifeIconSize * lifeIconScale;
        for (int i = 0; i < lifeImages.Length; i++)
        {
            Image icon = lifeImages[i];
            icon.sprite = lifeSprite;
            icon.rectTransform.sizeDelta = Vector2.one * iconSize;
            icon.rectTransform.anchoredPosition = new Vector2(i * (iconSize + lifeIconSpacing), 0f);
            icon.color = i < life ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);
            icon.enabled = true;
        }
    }

    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);

        return $"{minutes:00}:{seconds:00}";
    }
}
