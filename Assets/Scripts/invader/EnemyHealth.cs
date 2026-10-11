using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour, IPoolable, IDamageable
{
    public int maxHealth = 1; // 敵の最大体力
    [SerializeField]
    public int currentHealth;   // 敵の現在の体力
    private ObjectPool ownerPool; // 所属するオブジェクトプール
    private bool isDead = false; // 死亡フラグ
    private Animator anim; // アニメーターの参照
    private Rigidbody2D rb; // Rigidbody2Dの参照
    private EnemyMove enemyMove; // EnemyMoveの参照
    private EnemyAttack enemyAttack; // EnemyAttackの参照
    private EnemyGroupController enemyGroup; // 所属する敵グループ
    private Vector3 initialLocalScale;
    
    [Header("死亡音")]
    [SerializeField] private AudioClip deathSE; // 死亡音
    [Header("死亡エフェクトの再生秒数")]
    [SerializeField] private float playSeconds = 1.0f;

    private void Awake()
    {
        initialLocalScale = transform.localScale;
        currentHealth = maxHealth; // 初期化
        anim = GetComponent<Animator>(); // アニメーターの取得
        enemyMove = GetComponent<EnemyMove>(); // EnemyMoveの取得
        enemyAttack = GetComponent<EnemyAttack>(); // EnemyAttackの取得
        rb = GetComponent<Rigidbody2D>(); // Rigidbody2Dの取得
    }

    void Start()
    {

        anim.SetInteger("HP", currentHealth); // アニメーターに体力を渡す
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        if (currentHealth <= 0)
        {
            StartCoroutine(Die());
        }
    }

    public void SetPool(ObjectPool pool)
    {
        ownerPool = pool;
    }

    /// <summary>
    /// 敵にダメージを与えるメソッド
    /// </summary>
    /// <param name="damage">与えるダメージ量</param>
    public void TakeDamage(int damage)
    {
        ApplyDamage(damage, false);
    }

    /// <summary>
    /// プレイヤーの攻撃によるダメージを敵へ与える。
    /// </summary>
    /// <param name="damage">与えるダメージ量</param>
    public void TakePlayerDamage(int damage)
    {
        ApplyDamage(damage, true);
    }

    /// <summary>
    /// 敵が所属するグループを設定する。
    /// </summary>
    /// <param name="group">所属する敵グループ</param>
    public void SetEnemyGroup(EnemyGroupController group)
    {
        enemyGroup = group;
    }

    /// <summary>
    /// 撃破得点を加算せずに敵をプールへ戻す。
    /// </summary>
    public void DespawnWithoutDefeat()
    {
        StopAllCoroutines();

        if (ownerPool != null)
        {
            ownerPool.Return(gameObject);
            return;
        }

        enemyGroup?.UnregisterEnemy(this);
        enemyGroup = null;
        Destroy(gameObject);
    }

    /// <summary>
    /// 敵へダメージを適用する。
    /// </summary>
    /// <param name="damage">与えるダメージ量</param>
    /// <param name="isPlayerAttack">プレイヤーの攻撃によるダメージか</param>
    private void ApplyDamage(int damage, bool isPlayerAttack)
    {
        if (isDead || damage <= 0)
        {
            return;
        }

        currentHealth -= damage;
        anim.SetInteger("HP", currentHealth); // アニメーターに体力を渡す

        if (currentHealth > 0)
        {
            return;
        }

        if (isPlayerAttack)
        {
            enemyGroup?.RegisterEnemyDefeat(this);
        }

        StartCoroutine(Die());
    }

    /// <summary>
    /// 敵が死亡したときの処理
    /// </summary>
    private IEnumerator Die()
    {
        // 死亡エフェクトやスコア加算などの処理をここに追加
        if (isDead)
        {
            yield break;
        }

        isDead = true; // 死亡フラグを立てる

        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false; // 死亡時に当たり判定を無効化
        }

        if (deathSE != null)
        {
            AudioManager.Instance.PlaySE(deathSE); // 死亡音を再生
        }

        enemyMove.enabled = false; // 敵の移動を停止
        rb.linearVelocity = Vector2.zero; // Rigidbodyの速度をリセット

        yield return new WaitForSeconds(playSeconds);

        DespawnWithoutDefeat();
    }

    /// <summary>
    /// IPoolableインターフェースの実装
    /// </summary>
    public void OnSpawn()
    {
        StopAllCoroutines();
        enabled = true;
        isDead = false; // 死亡フラグをリセット
        enemyGroup = null;
        currentHealth = maxHealth; // スポーン時に体力をリセット

        if (enemyAttack != null)
        {
            enemyAttack.enabled = true;
        }

        if (anim != null)
        {
            anim.enabled = true;
            // プール再利用時に死亡状態の表示を残さない。
            anim.Rebind();
            anim.SetInteger("HP", currentHealth);
            anim.Update(0f);
        }

        // 前回の伸縮を新しいグループの基準サイズに引き継がない。
        transform.localScale = initialLocalScale;
        enemyMove.enabled = true; // 敵の移動を再開
        rb.linearVelocity = Vector2.zero; // Rigidbodyの速度をリセット
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = true; // コライダーを有効化
        }
    }

    /// <summary>
    /// IPoolableインターフェースの実装
    /// </summary>
    public void OnDespawn()
    {
        StopAllCoroutines();

        EnemyGroupController previousGroup = enemyGroup;
        enemyGroup = null;

        if (ownerPool != null && ownerPool.container != null)
        {
            // グループ破棄に巻き込まれないよう、登録解除より先に退避する。
            transform.SetParent(ownerPool.container, false);
            transform.position = ownerPool.container.position;
        }

        transform.localScale = initialLocalScale;
        previousGroup?.UnregisterEnemy(this);
        transform.rotation = Quaternion.identity; // 回転をリセット
    }
}
