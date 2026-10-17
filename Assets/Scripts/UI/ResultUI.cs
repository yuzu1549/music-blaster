using UnityEngine;
using MusicInvaders.Data;
using UnityEngine.UI;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;

public class ResultUI : MonoBehaviour
{
    [System.Serializable]
    public class ResultText
    {
        [Header("結果表示用のテキストオブジェクト")]
        public GameObject gameResultText;
        [Header("曲名表示用のテキストオブジェクト")]
        public GameObject songTitleText;
        [Header("難易度表示用のテキストオブジェクト")]
        public GameObject difficultyText;
        [Header("スコア表示用のテキストオブジェクト")]
        public GameObject scoreText;
        [Header("パーフェクト表示用のテキストオブジェクト")]
        public GameObject perfectText;
        [Header("グッド表示用のテキストオブジェクト")]
        public GameObject goodText;
        [Header("ミス表示用のテキストオブジェクト")]
        public GameObject missText;
        [Header("ランク表示用のテキストオブジェクト")]
        public GameObject rankText;
    }

    [System.Serializable]
    public class RankSprite
    {
        [Header("ランクSのスプライト")]
        public Sprite rankS;
        [Header("ランクAのスプライト")]
        public Sprite rankA;
        [Header("ランクBのスプライト")]
        public Sprite rankB;
        [Header("ランクCのスプライト")]
        public Sprite rankC;
        [Header("ランクDのスプライト")]
        public Sprite rankD;
    }

    [Header("結果表示用のテキスト")]
    [SerializeField]
    private ResultText resultText;

    [Header("ランク表示用のスプライト")]
    [SerializeField]
    private RankSprite rankSprites;

    [Header("ランク表示用のImage")]
    [SerializeField]
    private Image rankImage;

    [Header("曲データベース")]
    [SerializeField] private SongDatabaseSO songDatabase;

    [Header("共通のランク達成率設定")]
    [Tooltip("選曲画面と同じScoreRankSettingsを割り当ててください")]
    [SerializeField] private ScoreRankSettings rankSettings;

    [Header("リターンボタン")]
    [SerializeField]
    private GameObject returnButton;

    [Header("表示間隔")]
    [SerializeField, Min(0f)] private float displayInterval = 0.5f;

    [Header("数字の表示アニメーション")]
    [SerializeField, Min(0f)] private float numberAnimationDuration = 0.25f;
    [SerializeField, Range(0.1f, 1f)] private float numberShrinkScale = 0.8f;
    [SerializeField, Range(1f, 1.5f)] private float numberExpandScale = 1.1f;

    [Header("結果表示のSE")]
    [SerializeField] private AudioClip numberRevealSE;
    [SerializeField, Range(0f, 1f)] private float numberRevealVolume = 0.65f;
    [SerializeField] private AudioClip rankRevealSE;
    [SerializeField, Range(0f, 1f)] private float rankRevealVolume = 0.85f;


    private void Start()
    {
        rankImage.gameObject.SetActive(false);
        returnButton.SetActive(false);
        Result(this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow().Forget();
    }

    /// <summary>
    /// 結果を表示する
    /// </summary>
    private async UniTask Result(CancellationToken cancellationToken)
    {
        // ゲームオーバーまたはゲームクリアの状態を監視
        if (GameManager.Instance != null)
        {
            TitleTextUpdate();
            SongInfoTextUpdate();
            HighScoreUpdate();
            await ScoreJudgeUpdate(cancellationToken);

            await UniTask.Delay(TimeSpan.FromSeconds(1f), ignoreTimeScale: true,
                cancellationToken: cancellationToken);

            RankUpdate();
            returnButton.SetActive(true);
        }
    }

    /// <summary>
    /// タイトルテキストを更新する
    /// </summary>
    private void TitleTextUpdate()
    {
        if (GameManager.Instance.isGameOver)
        {
            // ゲームオーバー時の処理
            resultText.gameResultText.GetComponent<TMPro.TextMeshProUGUI>().text = "GAME OVER";
            resultText.gameResultText.GetComponent<TMPro.TextMeshProUGUI>().color = Color.red;
            GameManager.Instance.isGameOver = false; // フラグをリセット
            
        }
        else if (GameManager.Instance.isGameCleared)
        {
            // ゲームクリア時の処理
            resultText.gameResultText.GetComponent<TMPro.TextMeshProUGUI>().text = "GAME CLEAR";
            resultText.gameResultText.GetComponent<TMPro.TextMeshProUGUI>().color = Color.yellow;
            GameManager.Instance.isGameCleared = false; // フラグをリセット
        }
    }

    /// <summary>
    /// 曲名と難易度の表示を更新する
    /// </summary>
    private void SongInfoTextUpdate()
    {
        // 曲名と難易度の表示
        resultText.songTitleText.GetComponent<TMPro.TextMeshProUGUI>().text =
            SongDisplayName.Get(GameManager.Instance.musicTitle);
        resultText.difficultyText.GetComponent<TMPro.TextMeshProUGUI>().text = GameManager.Instance.difficulty;
    }

    /// <summary>
    /// スコア、パーフェクト、グッド、ミスの表示を更新する
    /// </summary>
    private async UniTask ScoreJudgeUpdate(CancellationToken cancellationToken)
    {
        // ラベルの設定
        const string perfectLabel = "<color=#ffff00>Perfect：</color>";
        const string goodLabel = "<color=#87cefa>Good：</color>";
        const string missLabel = "<color=#c0c0c0>Miss：</color>";
        const string scoreLabel = "<color=white>スコア：</color>";

        // 初期化
        TextMeshProUGUI perfectText = resultText.perfectText.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI goodText = resultText.goodText.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI missText = resultText.missText.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI scoreText = resultText.scoreText.GetComponent<TextMeshProUGUI>();
        int perfectCount = GameManager.Instance.perfectCount;
        int goodCount = GameManager.Instance.goodCount;
        int missCount = GameManager.Instance.missCount;
        int score = GameManager.Instance.score;

        perfectText.text = perfectLabel;
        goodText.text = goodLabel;
        missText.text = missLabel;
        scoreText.text = scoreLabel;

        // 数字の表示アニメーションを順番に実行
        await ShowNumberAsync(perfectText, perfectLabel, perfectCount, cancellationToken);
        await ShowNumberAsync(goodText, goodLabel, goodCount, cancellationToken);
        await ShowNumberAsync(missText, missLabel, missCount, cancellationToken);
        await ShowNumberAsync(scoreText, scoreLabel, score, cancellationToken);
    }

    /// <summary>
    /// 数字の表示アニメーションを実行する
    /// </summary>
    /// <param name="targetText"></param>
    /// <param name="label"></param>
    /// <param name="value"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async UniTask ShowNumberAsync(TextMeshProUGUI targetText, string label,
        int value, CancellationToken cancellationToken)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, displayInterval)),
            ignoreTimeScale: true, cancellationToken: cancellationToken);

        targetText.text = label + value;
        PlayResultSE(numberRevealSE, numberRevealVolume);
        float elapsedTime = 0f;

        try
        {
            while (elapsedTime < numberAnimationDuration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                float progress = elapsedTime / numberAnimationDuration;
                float scale = EvaluateNumberScale(progress);
                ScaleNumberVertices(targetText, label.Length, scale);

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                elapsedTime += Time.unscaledDeltaTime;
            }
        }
        finally
        {
            if (targetText != null)
            {
                targetText.ForceMeshUpdate();
            }
        }
    }

    /// <summary>
    /// 数字の拡縮率を評価する
    /// </summary>
    /// <param name="progress"></param>
    /// <returns></returns>
    private float EvaluateNumberScale(float progress)
    {
        if (progress < 0.3f)
        {
            return Mathf.SmoothStep(1f, numberShrinkScale, progress / 0.3f);
        }

        if (progress < 0.75f)
        {
            return Mathf.SmoothStep(numberShrinkScale, numberExpandScale,
                (progress - 0.3f) / 0.45f);
        }

        return Mathf.SmoothStep(numberExpandScale, 1f, (progress - 0.75f) / 0.25f);
    }

    /// <summary>
    /// 数字の頂点を拡縮する
    /// </summary>
    /// <param name="targetText"></param>
    /// <param name="numberStartIndex"></param>
    /// <param name="scale"></param>
    private static void ScaleNumberVertices(TextMeshProUGUI targetText,
        int numberStartIndex, float scale)
    {
        // 毎回元の頂点を生成し、拡縮の累積とレイアウト変更によるずれを防ぐ。
        targetText.ForceMeshUpdate();
        TMP_TextInfo textInfo = targetText.textInfo;
        Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
        Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);
        bool hasVisibleNumber = false;

        for (int characterIndex = 0; characterIndex < textInfo.characterCount; characterIndex++)
        {
            TMP_CharacterInfo character = textInfo.characterInfo[characterIndex];
            // indexはリッチテキストタグを含む元の文字列上の位置。
            if (!character.isVisible || character.index < numberStartIndex)
            {
                continue;
            }

            Vector3[] vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
            for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
            {
                Vector3 vertex = vertices[character.vertexIndex + cornerIndex];
                minimum = Vector3.Min(minimum, vertex);
                maximum = Vector3.Max(maximum, vertex);
            }

            hasVisibleNumber = true;
        }

        if (!hasVisibleNumber)
        {
            return;
        }

        Vector3 numberCenter = (minimum + maximum) * 0.5f;
        for (int characterIndex = 0; characterIndex < textInfo.characterCount; characterIndex++)
        {
            TMP_CharacterInfo character = textInfo.characterInfo[characterIndex];
            if (!character.isVisible || character.index < numberStartIndex)
            {
                continue;
            }

            Vector3[] vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
            for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
            {
                int vertexIndex = character.vertexIndex + cornerIndex;
                vertices[vertexIndex] = numberCenter + (vertices[vertexIndex] - numberCenter) * scale;
            }
        }

        targetText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    /// <summary>
    /// 最高スコアを更新する
    /// </summary>
    private void HighScoreUpdate()
    {
        int currentScore = GameManager.Instance.score;

        // 最高スコアの保存
        HighScoreStorage.TryUpdate(
            GameManager.Instance.musicTitle,
            GameManager.Instance.difficulty,
            currentScore
        );
    }

    /// <summary>
    /// ランクを更新する
    /// </summary>
    private void RankUpdate()
    {
        string rank = new ScoreRankCalculator().Calculate(
            GameManager.Instance.score,
            GameManager.Instance.maxScore,
            rankSettings);
        resultText.rankText.GetComponent<TMPro.TextMeshProUGUI>().text = "<color=white>ランク：</color>";
        switch (rank)
        {
            case "S":
                rankImage.sprite = rankSprites.rankS;
                break;
            case "A":
                rankImage.sprite = rankSprites.rankA;
                break;
            case "B":
                rankImage.sprite = rankSprites.rankB;
                break;
            case "C":
                rankImage.sprite = rankSprites.rankC;
                break;
            case "D":
                rankImage.sprite = rankSprites.rankD;
                break;
            default:
                Debug.LogWarning("不明なランク: " + rank);
                break;
        }
        rankImage.gameObject.SetActive(true);
        PlayResultSE(rankRevealSE, rankRevealVolume);
    }

    private static void PlayResultSE(AudioClip clip, float volume)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySE(clip, volume);
        }
    }

    /// <summary>
    /// 結果画面が閉じられるときにスコアをリセットする
    /// </summary>
    private void OnDestroy()
    {
        if (GameManager.Instance == null)
        {
            return;
        }
        // 結果画面が閉じられるときにスコアをリセット
        GameManager.Instance.score = 0;
        GameManager.Instance.perfectCount = 0;
        GameManager.Instance.goodCount = 0;
        GameManager.Instance.missCount = 0;
    }
}
