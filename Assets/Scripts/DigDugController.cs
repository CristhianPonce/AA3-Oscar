using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
public class DigDugController : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private LayerMask hardGroundLayer;

    [Tooltip("Tiempo que hay que mantener una dirección nueva antes de empezar a caminar. Permite pulsar una vez para solo girar, estilo Pokémon.")]
    [SerializeField] private float turnHoldTimeBeforeMove = 0.12f;

    [Header("Sprites idle de LINFO")]
    [SerializeField] private Sprite idleUpSprite;
    [SerializeField] private Sprite idleDownSprite;
    [SerializeField] private Sprite idleLeftSprite;
    [SerializeField] private Sprite idleRightSprite;

    [Header("Configuración del Aguijón / Arpón")]
    [SerializeField] private float attackRange = 4f;
    [SerializeField] private float freeHarpoonReturnTime = 0.15f;
    [SerializeField] private LayerMask enemyLayer;

    [Tooltip("Capas que detienen el disparo: tierra tumoral sin excavar, muros duros, rocas, etc.")]
    [SerializeField] private LayerMask harpoonObstacleLayer;

    [SerializeField] private LineRenderer lineRenderer;

    [Tooltip("Radio de detección del aguijón. Hace que el disparo sea más permisivo y no falle por pocos píxeles.")]
    [SerializeField] private float harpoonHitRadius = 0.18f;

    [Tooltip("Origen opcional del disparo. Si no se asigna, se usa automáticamente el centro del collider/sprite de LINFO.")]
    [SerializeField] private Transform harpoonOrigin;

    [Header("Configuración de Munición")]
    [SerializeField] private int maxHarpoons = 20;
    public int currentHarpoons;

    private Rigidbody2D rb;
    private Vector2 moveDirection = Vector2.down;
    private Vector2 currentInputDirection;
    private Vector2 targetPosition;
    private bool isMoving;
    private bool isAttacking;
    private EnemyBase harpoonedEnemy;
    private Coroutine returnHarpoonCoroutine;
    private Collider2D playerCollider;
    private SpriteRenderer playerSpriteRenderer;

    private bool waitingForHoldAfterTurn;
    private Vector2 pendingTurnDirection;
    private float pendingTurnHoldTimer;

    public Vector2 FacingDirection => moveDirection;

    private void Awake()
    {
        EnsurePlayerTag();
        if (GetComponent<PlayerDeath>() == null)
        {
            gameObject.AddComponent<PlayerDeath>();
        }
        FindReferencesIfMissing();
        playerCollider = GetComponent<Collider2D>();
        playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        targetPosition = transform.position;
        currentHarpoons = maxHarpoons;

        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }

        ApplyIdleSpriteForDirection(moveDirection);
        TryDigAt(transform.position);
    }

    private void Update()
    {
        if (LinfoCameraController.IsMapViewActive)
        {
            FreezeMovementForMapView();
            UpdateAttachedHarpoonLine();
            return;
        }

        HandleHarpoonInput();
        UpdateAttachedHarpoonLine();

        if (!isAttacking)
        {
            HandleMovementInput();
        }
    }

    private void FixedUpdate()
    {
        if (LinfoCameraController.IsMapViewActive)
        {
            FreezeMovementForMapView();
            return;
        }

        if (!isMoving || isAttacking)
        {
            return;
        }

        TryDigAt(targetPosition);

        Vector2 newPosition = Vector2.MoveTowards(rb.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(newPosition);

        if (Vector2.Distance(rb.position, targetPosition) < 0.05f)
        {
            rb.position = targetPosition;
            isMoving = false;
        }
    }

    private void HandleHarpoonInput()
    {
        if (Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return;
        }

        if (harpoonedEnemy != null)
        {
            harpoonedEnemy.PumpFromHarpoon();
            return;
        }

        if (currentHarpoons <= 0)
        {
            Debug.Log("LINFO se ha quedado sin cargas de aguijón.");
            return;
        }

        if (isMoving || isAttacking)
        {
            return;
        }

        ShootHarpoon();
    }

    private void HandleMovementInput()
    {
        if (isMoving || Keyboard.current == null)
        {
            return;
        }

        if (!TryReadMovementInput(out Vector2 inputDirection, out Vector2 stepVector))
        {
            currentInputDirection = Vector2.zero;
            ResetTurnHoldBuffer();
            return;
        }

        currentInputDirection = stepVector;

        if (waitingForHoldAfterTurn)
        {
            if (!IsSameDirection(inputDirection, pendingTurnDirection))
            {
                ResetTurnHoldBuffer();
            }
            else
            {
                pendingTurnHoldTimer += Time.deltaTime;
                if (pendingTurnHoldTimer < turnHoldTimeBeforeMove)
                {
                    return;
                }

                ResetTurnHoldBuffer();
                TryStartStep(stepVector);
                return;
            }
        }

        if (!IsSameDirection(inputDirection, moveDirection))
        {
            FaceDirection(inputDirection);
            waitingForHoldAfterTurn = true;
            pendingTurnDirection = inputDirection;
            pendingTurnHoldTimer = 0f;
            return;
        }

        TryStartStep(stepVector);
    }

    private bool TryReadMovementInput(out Vector2 inputDirection, out Vector2 stepVector)
    {
        inputDirection = Vector2.zero;
        stepVector = Vector2.zero;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        float cellSizeX = groundTilemap != null ? groundTilemap.cellSize.x : 1f;
        float cellSizeY = groundTilemap != null ? groundTilemap.cellSize.y : 1f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            inputDirection = Vector2.right;
            stepVector = new Vector2(cellSizeX, 0f);
            return true;
        }

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            inputDirection = Vector2.left;
            stepVector = new Vector2(-cellSizeX, 0f);
            return true;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            inputDirection = Vector2.up;
            stepVector = new Vector2(0f, cellSizeY);
            return true;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            inputDirection = Vector2.down;
            stepVector = new Vector2(0f, -cellSizeY);
            return true;
        }

        return false;
    }

    private void TryStartStep(Vector2 stepVector)
    {
        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 potentialTarget = origin + stepVector;

        if (CanMove(potentialTarget))
        {
            targetPosition = potentialTarget;
            isMoving = true;
        }
        else
        {
            currentInputDirection = Vector2.zero;
            isMoving = false;
        }
    }

    private void FaceDirection(Vector2 direction)
    {
        if (direction == Vector2.zero)
        {
            return;
        }

        moveDirection = direction.normalized;
        ApplyIdleSpriteForDirection(moveDirection);
    }

    private void ApplyIdleSpriteForDirection(Vector2 direction)
    {
        if (playerSpriteRenderer == null)
        {
            return;
        }

        Sprite selectedSprite = null;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            selectedSprite = direction.x > 0f ? idleRightSprite : idleLeftSprite;
        }
        else
        {
            selectedSprite = direction.y > 0f ? idleUpSprite : idleDownSprite;
        }

        if (selectedSprite != null)
        {
            playerSpriteRenderer.sprite = selectedSprite;
        }
    }

    private bool IsSameDirection(Vector2 first, Vector2 second)
    {
        if (first == Vector2.zero || second == Vector2.zero)
        {
            return false;
        }

        return Vector2.Dot(first.normalized, second.normalized) > 0.99f;
    }

    private void ResetTurnHoldBuffer()
    {
        waitingForHoldAfterTurn = false;
        pendingTurnDirection = Vector2.zero;
        pendingTurnHoldTimer = 0f;
    }

    private bool CanMove(Vector2 targetPos)
    {
        Collider2D hit = Physics2D.OverlapPoint(targetPos, hardGroundLayer);
        return hit == null;
    }

    private void FreezeMovementForMapView()
    {
        currentInputDirection = Vector2.zero;
        isMoving = false;
        ResetTurnHoldBuffer();

        Vector2 currentPosition = rb != null ? rb.position : (Vector2)transform.position;
        targetPosition = currentPosition;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void TryDigAt(Vector2 worldPosition)
    {
        if (groundTilemap == null)
        {
            return;
        }

        Vector3Int tilePosition = groundTilemap.WorldToCell(worldPosition);
        if (groundTilemap.HasTile(tilePosition))
        {
            groundTilemap.SetTile(tilePosition, null);
            TumorGameManager.Instance?.OnInnocentCellDug(tilePosition);
        }
    }

    private void ShootHarpoon()
    {
        isAttacking = true;
        currentHarpoons--;

        Vector2 origin = GetHarpoonOrigin();
        Vector2 direction = moveDirection.normalized;
        if (direction == Vector2.zero)
        {
            direction = Vector2.right;
        }

        int hitMask = enemyLayer.value | harpoonObstacleLayer.value;
        RaycastHit2D hit = CastHarpoon(origin, direction, hitMask);

        Vector2 endPoint = hit.collider != null
            ? GetHarpoonEndPoint(origin, direction, hit)
            : origin + direction * attackRange;

        ShowHarpoon(origin, endPoint);

        if (hit.collider != null)
        {
            TumorCell tumorCell = hit.collider.GetComponentInParent<TumorCell>();
            if (tumorCell != null)
            {
                tumorCell.ReceiveLinfoAttack();
                returnHarpoonCoroutine = StartCoroutine(ReturnFreeHarpoon());
                return;
            }

            EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
            if (enemy != null && enemy.AttachHarpoon(this))
            {
                harpoonedEnemy = enemy;
                UpdateAttachedHarpoonLine();
                return;
            }
        }

        returnHarpoonCoroutine = StartCoroutine(ReturnFreeHarpoon());
    }

    private RaycastHit2D CastHarpoon(Vector2 origin, Vector2 direction, int hitMask)
    {
        if (harpoonHitRadius > 0f)
        {
            return hitMask != 0
                ? Physics2D.CircleCast(origin, harpoonHitRadius, direction, attackRange, hitMask)
                : Physics2D.CircleCast(origin, harpoonHitRadius, direction, attackRange);
        }

        return hitMask != 0
            ? Physics2D.Raycast(origin, direction, attackRange, hitMask)
            : Physics2D.Raycast(origin, direction, attackRange);
    }

    private Vector2 GetHarpoonEndPoint(Vector2 origin, Vector2 direction, RaycastHit2D hit)
    {
        if (hit.distance > 0f)
        {
            return origin + direction * hit.distance;
        }

        if (hit.point != Vector2.zero)
        {
            return hit.point;
        }

        return origin + direction * attackRange;
    }

    private Vector2 GetHarpoonOrigin()
    {
        if (harpoonOrigin != null)
        {
            return harpoonOrigin.position;
        }

        if (playerCollider != null)
        {
            return playerCollider.bounds.center;
        }

        if (playerSpriteRenderer != null)
        {
            return playerSpriteRenderer.bounds.center;
        }

        return transform.position;
    }

    private IEnumerator ReturnFreeHarpoon()
    {
        yield return new WaitForSeconds(freeHarpoonReturnTime);
        returnHarpoonCoroutine = null;
        RetrieveHarpoon();
    }

    private void ShowHarpoon(Vector2 origin, Vector2 endPoint)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, endPoint);
    }

    private void UpdateAttachedHarpoonLine()
    {
        if (harpoonedEnemy == null || lineRenderer == null)
        {
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, GetHarpoonOrigin());
        lineRenderer.SetPosition(1, harpoonedEnemy.transform.position);
    }

    public void RecoverHarpoonFromEnemy(EnemyBase enemy)
    {
        if (harpoonedEnemy != enemy)
        {
            return;
        }

        harpoonedEnemy = null;
        RetrieveHarpoon();
    }

    private void RetrieveHarpoon()
    {
        if (returnHarpoonCoroutine != null)
        {
            StopCoroutine(returnHarpoonCoroutine);
            returnHarpoonCoroutine = null;
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }

        harpoonedEnemy = null;
        isAttacking = false;
    }

    private void FindReferencesIfMissing()
    {
        if (groundTilemap == null)
        {
            Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            foreach (Tilemap tilemap in tilemaps)
            {
                if (tilemap.name.Equals("Ground", System.StringComparison.OrdinalIgnoreCase))
                {
                    groundTilemap = tilemap;
                    break;
                }
            }

            if (groundTilemap == null && tilemaps.Length > 0)
            {
                groundTilemap = tilemaps[0];
            }
        }

        if (lineRenderer == null)
        {
            lineRenderer = FindFirstObjectByType<LineRenderer>();
        }

        if (hardGroundLayer.value == 0)
        {
            hardGroundLayer = LayerMask.GetMask("HardGround");
        }

        if (enemyLayer.value == 0)
        {
            enemyLayer = LayerMask.GetMask("Enemy");
        }

        if (harpoonObstacleLayer.value == 0)
        {
            harpoonObstacleLayer = LayerMask.GetMask("Ground", "HardGround", "Rock");
        }
    }

    private void EnsurePlayerTag()
    {
        if (CompareTag("Player"))
        {
            return;
        }

        try
        {
            gameObject.tag = "Player";
        }
        catch
        {
            Debug.LogWarning("[LINFO] No existe el tag Player. Añádelo en Project Settings > Tags and Layers para que los contactos funcionen correctamente.");
        }
    }
}
