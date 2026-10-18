using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [Header("スポーンする弾の名前")]
    [SerializeField] private string bulletName = "EnemyBullet";

    [Header("弾の色コード")]
    [Tooltip("#RRGGBB または #RRGGBBAA 形式で指定してください（例: #b44141）")]
    [SerializeField] private string bulletColorCode = "#ff8c57";

    private Color bulletColor = Color.blue;

    /// <summary>
    /// 色コードを解析して弾の表示色を初期化する。
    /// </summary>
    private void Awake()
    {
        ApplyBulletColorCode();
    }

    /// <summary>
    /// Inspector で変更した色コードを弾の表示色へ反映する。
    /// </summary>
    private void OnValidate()
    {
        ApplyBulletColorCode();
    }

    /// <summary>
    /// 色コードを表示色に変換し、無効な場合は赤を使用する。
    /// </summary>
    private void ApplyBulletColorCode()
    {
        if (!ColorUtility.TryParseHtmlString(bulletColorCode, out bulletColor))
        {
            bulletColor = Color.red;
            Debug.LogWarning($"{name}: 弾の色コード {bulletColorCode} が無効なため、#FF0000 を使用します。");
        }
    }

    /// <summary>
    /// プールから弾を取得し、設定した色を反映して発射する。
    /// </summary>
    public void Attack()
    {
        if (string.IsNullOrEmpty(bulletName))
        {
            Debug.LogError($"{name}: bulletName が設定されていません。");
            return;
        }

        GameObject spawnedBullet = PoolManager.Instance.Get(bulletName);
        if (spawnedBullet == null)
        {
            Debug.LogError($"{name}: プールから {bulletName} を取得できませんでした。");
            return;
        }

        spawnedBullet.transform.position = transform.position;
        if (spawnedBullet.TryGetComponent(out SpriteRenderer bulletSpriteRenderer))
        {
            bulletSpriteRenderer.color = bulletColor;
        }

        spawnedBullet.GetComponent<EnemyBullet>().Attack();
    }
}
