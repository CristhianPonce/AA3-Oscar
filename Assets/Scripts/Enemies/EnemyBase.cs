using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Configuración Base de Enemigo")]
    [SerializeField] protected float normalSpeed = 2f;
    [SerializeField] protected float ghostSpeed = 1f;
    [SerializeField] protected float ghostModeDuration = 5f;

    [Header("Detección de túneles")]
    [SerializeField] protected LayerMask groundLayer;
    [SerializeField] protected float wallCheckDistance = 0.6f;

    [Header("Configuración del Arpón")]
    [SerializeField] protected float harpoonEscapeDelay = 1f;
    [SerializeField] protected int maxInflateStage = 4;
    [SerializeField] protected float inflateScalePerStage = 0.25f;

    protected Rigidbody2D rb;
    protected Collider2D col;
    protected SpriteRenderer spriteRenderer;
    protected Transform player;

    protected bool isGhost;
    protected Vector2 currentDirection = Vector2.right;

    protected bool isDead;
    protected bool isHarpooned;
    protected int inflateStage;
    protected float harpoonTimer;
    protected DigDugController harpoonOwner;

    protected static readonly Vector2[] Directions =
    {
        Vector2.right,
        Vector2.left,
        Vector2.up,
        Vector2.down
    };

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    protected virtual void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            Debug.LogError("No se ha encontrado ningún GameObject con tag Player.");
            enabled = false;
            return;
        }

        player = playerObject.transform;

        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        StartCoroutine(GhostModeRoutine());
    }

    protected virtual void Update()
    {
        if (!isHarpooned || isDead)
        {
            return;
        }

        harpoonTimer -= Time.deltaTime;

        if (harpoonTimer <= 0f)
        {
            EscapeFromHarpoon();
        }
    }

    protected virtual void FixedUpdate()
    {
        if (isDead)
        {
            return;
        }

        if (isHarpooned)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isGhost)
        {
            MoveAsGhost();
        }
        else
        {
            MoveInTunnels();
        }
    }

    protected virtual void MoveInTunnels()
    {
        Vector2 bestDirection = FindBestTunnelDirection();

        if (bestDirection == Vector2.zero)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        currentDirection = bestDirection;
        rb.linearVelocity = currentDirection * normalSpeed;
    }

    private Vector2 FindBestTunnelDirection()
    {
        if (player == null)
        {
            return currentDirection;
        }

        Vector2 directionToPlayer =
            ((Vector2)player.position - rb.position).normalized;

        Vector2 bestDirection = Vector2.zero;
        float bestScore = float.NegativeInfinity;

        foreach (Vector2 direction in Directions)
        {
            if (!CanMoveInDirection(direction))
            {
                continue;
            }

            float score = Vector2.Dot(direction, directionToPlayer);

            if (direction == -currentDirection)
            {
                score -= 0.2f;
            }

            if (direction == currentDirection)
            {
                score += 0.05f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = direction;
            }
        }

        return bestDirection;
    }

    private bool CanMoveInDirection(Vector2 direction)
    {
        RaycastHit2D hit = Physics2D.Raycast(
            rb.position,
            direction,
            wallCheckDistance,
            groundLayer
        );

        Debug.DrawRay(
            rb.position,
            direction * wallCheckDistance,
            hit.collider != null ? Color.red : Color.green
        );

        return hit.collider == null;
    }

    protected virtual void MoveAsGhost()
    {
        if (player == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 directionToPlayer =
            ((Vector2)player.position - rb.position).normalized;

        rb.linearVelocity = directionToPlayer * ghostSpeed;
    }

    public bool AttachHarpoon(DigDugController owner)
    {
        if (isDead || isHarpooned)
        {
            return false;
        }

        harpoonOwner = owner;
        isHarpooned = true;
        harpoonTimer = harpoonEscapeDelay;

        rb.linearVelocity = Vector2.zero;

        return true;
    }

    public void PumpFromHarpoon()
    {
        if (!isHarpooned || isDead)
        {
            return;
        }

        harpoonTimer = harpoonEscapeDelay;
        inflateStage++;

        transform.localScale =
            Vector3.one * (1f + inflateStage * inflateScalePerStage);

        rb.linearVelocity = Vector2.zero;

        if (inflateStage >= maxInflateStage)
        {
            Die();
        }
    }

    private void EscapeFromHarpoon()
    {
        if (!isHarpooned || isDead)
        {
            return;
        }

        isHarpooned = false;
        inflateStage = 0;
        transform.localScale = Vector3.one;

        DigDugController owner = harpoonOwner;
        harpoonOwner = null;

        owner?.RecoverHarpoonFromEnemy(this);
    }

    private IEnumerator GhostModeRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(10f, 15f));

            SetGhostMode(true);

            yield return new WaitForSeconds(ghostModeDuration);

            SetGhostMode(false);
        }
    }

    private void SetGhostMode(bool ghostEnabled)
    {
        isGhost = ghostEnabled;
        col.isTrigger = ghostEnabled;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = ghostEnabled
                ? new Color(1f, 1f, 1f, 0.6f)
                : Color.white;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryKillPlayer(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryKillPlayer(other);
    }

    private void TryKillPlayer(Collider2D other)
    {
        PlayerDeath playerDeath = other.GetComponentInParent<PlayerDeath>();

        if (playerDeath != null)
        {
            playerDeath.Die();
        }
    }

    protected virtual void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        DigDugController owner = harpoonOwner;
        harpoonOwner = null;
        isHarpooned = false;

        owner?.RecoverHarpoonFromEnemy(this);

        StopAllCoroutines();
        Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (!isDead && harpoonOwner != null)
        {
            harpoonOwner.RecoverHarpoonFromEnemy(this);
        }
    }
}