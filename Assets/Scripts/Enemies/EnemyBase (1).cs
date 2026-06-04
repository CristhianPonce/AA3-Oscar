using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Configuración Base de Enemigo")]
    [SerializeField] protected float normalSpeed = 2f;
    [SerializeField] protected float ghostSpeed = 2f;
    [SerializeField] protected float ghostModeDuration = 5f;

    [Header("Grid y Túneles")]
    [SerializeField] protected float tileSize = 1f;
    [SerializeField] protected float gridSnapThreshold = 0.1f;

    [Tooltip("Arrastra AQUÍ el Tilemap específico que representa los túneles transitables.")]
    [SerializeField] protected Tilemap tunnelTilemap;

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
    protected Vector2 desiredDirection = Vector2.right;

    // Destino fijo del modo fantasma (punto donde estaba el jugador al activarse)
    private Vector2 ghostTarget;
    private bool ghostTargetReached;

    protected bool isDead;
    protected bool isHarpooned;
    protected int inflateStage;
    protected float harpoonTimer;
    protected DigDugController harpoonOwner;

    // Guardado de la escala original para el inflado proporcional
    private Vector3 initialScale;

    protected static readonly Vector2[] Directions =
    {
        Vector2.right, Vector2.left, Vector2.up, Vector2.down
    };

    // ─────────────────────────────────────────────
    //  Inicialización
    // ─────────────────────────────────────────────

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
            Debug.LogError("No se encontró ningún GameObject con tag 'Player'.");
            enabled = false;
            return;
        }

        player = playerObject.transform;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Guardamos la escala inicial (pequeña) configurada en el Inspector
        initialScale = transform.localScale;

        // Búsqueda automática inteligente del Tilemap de túneles si no está asignado
        if (tunnelTilemap == null)
        {
            foreach (Tilemap tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                if (tm.gameObject.name.ToLower().Contains("tunnel") || tm.gameObject.layer == LayerMask.NameToLayer("Tunnel"))
                {
                    tunnelTilemap = tm;
                    break;
                }
            }

            if (tunnelTilemap == null)
            {
                tunnelTilemap = FindFirstObjectByType<Tilemap>();
                if (tunnelTilemap != null)
                    Debug.LogWarning($"[EnemyBase] Asignado automáticamente '{tunnelTilemap.name}'. Si este no es el de los túneles, arrástralo manualmente al Inspector.");
                else
                    Debug.LogError("[EnemyBase] ¡No se encontró ningún Tilemap en la escena!");
            }
        }

        currentDirection = ChooseInitialDirection();
        desiredDirection = currentDirection;

        StartCoroutine(GhostModeRoutine());
    }

    // ─────────────────────────────────────────────
    //  Update / FixedUpdate
    // ─────────────────────────────────────────────

    protected virtual void Update()
    {
        if (isDead) return;

        if (isHarpooned)
        {
            harpoonTimer -= Time.deltaTime;
            if (harpoonTimer <= 0f) EscapeFromHarpoon();
        }
    }

    protected virtual void FixedUpdate()
    {
        if (isDead) return;

        if (isHarpooned)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isGhost)
            MoveAsGhost();
        else
            MoveInTunnels();
    }

    // ─────────────────────────────────────────────
    //  Movimiento por túneles
    // ─────────────────────────────────────────────

    protected virtual void MoveInTunnels()
    {
        UpdateDesiredDirection();

        if (IsAlignedWithGrid())
        {
            if (CanMoveInDirection(desiredDirection))
                currentDirection = desiredDirection;
            else if (!CanMoveInDirection(currentDirection))
            {
                Vector2 fallback = FindAnyValidDirection();
                if (fallback != Vector2.zero)
                    currentDirection = fallback;
                else
                {
                    rb.linearVelocity = Vector2.zero;
                    return;
                }
            }

            SnapToGrid();
        }

        rb.linearVelocity = currentDirection * normalSpeed;
    }

    protected virtual void UpdateDesiredDirection()
    {
        if (player == null) return;

        Vector2 toPlayer = ((Vector2)player.position - rb.position).normalized;
        Vector2 best = currentDirection;
        float bestScore = float.NegativeInfinity;

        foreach (Vector2 dir in Directions)
        {
            if (dir == -currentDirection) continue;
            if (!CanMoveInDirection(dir)) continue;

            float score = Vector2.Dot(dir, toPlayer);
            if (dir == currentDirection) score += 0.1f;

            if (score > bestScore)
            {
                bestScore = score;
                best = dir;
            }
        }

        desiredDirection = best;
    }

    // ─────────────────────────────────────────────
    //  Detección de túneles con Tilemap
    // ─────────────────────────────────────────────

    protected virtual bool CanMoveInDirection(Vector2 direction)
    {
        // Si no hay mapa asignado, no permitimos movimiento por seguridad
        if (tunnelTilemap == null) return false;

        // Calculamos la posición central de la siguiente celda
        Vector2 checkWorldPos = rb.position + direction * tileSize;
        Vector3Int cellPos = tunnelTilemap.WorldToCell(checkWorldPos);

        // Comprobamos si existe un Tile físico en esa posición del Tilemap de túneles
        // NOTA: Si tus túneles son "huecos vacíos" borrados de la tierra, cambia esto por: !tunnelTilemap.HasTile(cellPos)
        bool hasTile = tunnelTilemap.HasTile(cellPos);

        // Debug visual en la ventana de Escena
        Debug.DrawRay(rb.position, direction * tileSize, hasTile ? Color.green : Color.red);

        return hasTile;
    }

    // ─────────────────────────────────────────────
    //  Modo fantasma: va al punto fijo donde estaba el jugador
    // ─────────────────────────────────────────────

    protected virtual void MoveAsGhost()
    {
        if (ghostTargetReached)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toTarget = ghostTarget - rb.position;

        // Si llega al destino, se detiene
        if (toTarget.magnitude < 0.15f)
        {
            rb.position = ghostTarget;
            rb.linearVelocity = Vector2.zero;
            ghostTargetReached = true;
            return;
        }

        rb.linearVelocity = toTarget.normalized * ghostSpeed;
    }

    // ─────────────────────────────────────────────
    //  Corrutina del modo fantasma
    // ─────────────────────────────────────────────

    private IEnumerator GhostModeRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(10f, 15f));

            // Capturamos la posición del jugador en este instante preciso
            if (player != null)
                ghostTarget = player.position;
            else
                ghostTarget = rb.position;

            ghostTargetReached = false;

            SetGhostMode(true);

            // Esperamos hasta que llegue al destino O se agote el tiempo de duración
            float elapsed = 0f;
            while (elapsed < ghostModeDuration && !ghostTargetReached)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetGhostMode(false);

            // Volver al grid de forma limpia al recuperar su estado físico
            SnapToGrid();
            currentDirection = ChooseInitialDirection();
            desiredDirection = currentDirection;
        }
    }

    private void SetGhostMode(bool ghostEnabled)
    {
        isGhost = ghostEnabled;
        col.isTrigger = ghostEnabled;

        if (spriteRenderer != null)
            spriteRenderer.color = ghostEnabled ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
    }

    // ─────────────────────────────────────────────
    //  Utilidades de grid
    // ─────────────────────────────────────────────

    private bool IsAlignedWithGrid()
    {
        float remX = Mathf.Abs(rb.position.x % tileSize);
        float remY = Mathf.Abs(rb.position.y % tileSize);
        if (remX > tileSize * 0.5f) remX = tileSize - remX;
        if (remY > tileSize * 0.5f) remY = tileSize - remY;
        return remX < gridSnapThreshold && remY < gridSnapThreshold;
    }

    private void SnapToGrid()
    {
        Vector2 pos = rb.position;
        pos.x = Mathf.Round(pos.x / tileSize) * tileSize;
        pos.y = Mathf.Round(pos.y / tileSize) * tileSize;
        rb.position = pos;
    }

    private Vector2 FindAnyValidDirection()
    {
        foreach (Vector2 dir in Directions)
        {
            if (dir == -currentDirection) continue;
            if (CanMoveInDirection(dir)) return dir;
        }
        if (CanMoveInDirection(-currentDirection)) return -currentDirection;
        return Vector2.zero;
    }

    private Vector2 ChooseInitialDirection()
    {
        foreach (Vector2 dir in Directions)
            if (CanMoveInDirection(dir)) return dir;
        return Vector2.right;
    }

    // ─────────────────────────────────────────────
    //  Arpón (Mecánicas de Inflado Corregidas)
    // ─────────────────────────────────────────────

    public bool AttachHarpoon(DigDugController owner)
    {
        if (isDead || isHarpooned) return false;
        harpoonOwner = owner;
        isHarpooned = true;
        harpoonTimer = harpoonEscapeDelay;
        rb.linearVelocity = Vector2.zero;
        return true;
    }

    public void PumpFromHarpoon()
    {
        if (!isHarpooned || isDead) return;
        harpoonTimer = harpoonEscapeDelay;
        inflateStage++;

        // Multiplica el multiplicador de tamaño por la escala inicial pequeña guardada
        transform.localScale = initialScale * (1f + inflateStage * inflateScalePerStage);

        rb.linearVelocity = Vector2.zero;
        if (inflateStage >= maxInflateStage) Die();
    }

    private void EscapeFromHarpoon()
    {
        if (!isHarpooned || isDead) return;
        isHarpooned = false;
        inflateStage = 0;

        // Restaura exactamente su escala original pequeña
        transform.localScale = initialScale;

        DigDugController owner = harpoonOwner;
        harpoonOwner = null;
        owner?.RecoverHarpoonFromEnemy(this);
    }

    // ─────────────────────────────────────────────
    //  Contacto con el jugador
    // ─────────────────────────────────────────────

    private void OnCollisionEnter2D(Collision2D collision) => TryKillPlayer(collision.collider);
    private void OnTriggerEnter2D(Collider2D other) => TryKillPlayer(other);

    private void TryKillPlayer(Collider2D other)
    {
        if (isDead) return;
        other.GetComponentInParent<PlayerDeath>()?.Die();
    }

    // ─────────────────────────────────────────────
    //  Muerte
    // ─────────────────────────────────────────────

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;
        isHarpooned = false;
        DigDugController owner = harpoonOwner;
        harpoonOwner = null;
        owner?.RecoverHarpoonFromEnemy(this);
        StopAllCoroutines();
        Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (!isDead && harpoonOwner != null)
            harpoonOwner.RecoverHarpoonFromEnemy(this);
    }
}