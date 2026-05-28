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

    [Header("Configuración del Arpón")]
    [SerializeField] private float attackRange = 4f;
    [SerializeField] private float freeHarpoonReturnTime = 0.15f;
    [SerializeField] private LayerMask enemyLayer;

    // Capas que detienen el arpón: muros, piedras, tierra sin excavar, etc.
    [SerializeField] private LayerMask harpoonObstacleLayer;

    [SerializeField] private LineRenderer lineRenderer;

    private Rigidbody2D rb;

    private Vector2 moveDirection = Vector2.right;
    private Vector2 currentInputDirection;
    private Vector2 targetPosition;

    private bool isMoving;
    private bool isAttacking;

    private EnemyBase harpoonedEnemy;
    private Coroutine returnHarpoonCoroutine;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        targetPosition = transform.position;

        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }
    }

    private void Update()
    {
        HandleHarpoonInput();
        UpdateAttachedHarpoonLine();

        if (isAttacking)
        {
            return;
        }

        HandleMovementInput();
    }

    private void FixedUpdate()
    {
        if (!isMoving || isAttacking)
        {
            return;
        }

        TryDig();

        Vector2 newPosition = Vector2.MoveTowards(
            rb.position,
            targetPosition,
            moveSpeed * Time.fixedDeltaTime
        );

        rb.MovePosition(newPosition);

        if (Vector2.Distance(rb.position, targetPosition) < 0.05f)
        {
            rb.position = targetPosition;
            isMoving = false;
        }
    }

    private void HandleHarpoonInput()
    {
        if (Keyboard.current == null ||
            !Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return;
        }

        // Si el arpón ya está clavado, cada pulsación infla al enemigo.
        if (harpoonedEnemy != null)
        {
            harpoonedEnemy.PumpFromHarpoon();
            return;
        }

        // Mientras está caminando entre casillas o el arpón está volviendo,
        // no puede lanzar uno nuevo.
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

        float moveX = 0f;
        float moveY = 0f;

        if (Keyboard.current.dKey.isPressed ||
            Keyboard.current.rightArrowKey.isPressed)
        {
            moveX = groundTilemap.cellSize.x;
        }
        else if (Keyboard.current.aKey.isPressed ||
                 Keyboard.current.leftArrowKey.isPressed)
        {
            moveX = -groundTilemap.cellSize.x;
        }

        if (moveX == 0f)
        {
            if (Keyboard.current.wKey.isPressed ||
                Keyboard.current.upArrowKey.isPressed)
            {
                moveY = groundTilemap.cellSize.y;
            }
            else if (Keyboard.current.sKey.isPressed ||
                     Keyboard.current.downArrowKey.isPressed)
            {
                moveY = -groundTilemap.cellSize.y;
            }
        }

        if (moveX != 0f)
        {
            currentInputDirection = new Vector2(moveX, 0f);
        }
        else if (moveY != 0f)
        {
            currentInputDirection = new Vector2(0f, moveY);
        }
        else
        {
            currentInputDirection = Vector2.zero;
        }

        if (currentInputDirection == Vector2.zero)
        {
            return;
        }

        moveDirection = currentInputDirection.normalized;

        Vector2 potentialTarget =
            (Vector2)transform.position + currentInputDirection;

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

    private bool CanMove(Vector2 targetPos)
    {
        Collider2D hit = Physics2D.OverlapPoint(targetPos, hardGroundLayer);
        return hit == null;
    }

    private void TryDig()
    {
        if (groundTilemap == null)
        {
            return;
        }

        Vector3Int tilePosition =
            groundTilemap.WorldToCell((Vector3)targetPosition);

        if (groundTilemap.HasTile(tilePosition))
        {
            groundTilemap.SetTile(tilePosition, null);
        }
    }

    private void ShootHarpoon()
    {
        isAttacking = true;

        int hitMask = enemyLayer.value | harpoonObstacleLayer.value;

        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            moveDirection,
            attackRange,
            hitMask
        );

        Vector2 endPoint = hit.collider != null
            ? hit.point
            : (Vector2)transform.position + moveDirection * attackRange;

        ShowHarpoon(endPoint);

        if (hit.collider != null)
        {
            EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();

            if (enemy != null && enemy.AttachHarpoon(this))
            {
                harpoonedEnemy = enemy;
                UpdateAttachedHarpoonLine();
                return;
            }
        }

        // Si no ha impactado contra un enemigo, vuelve automáticamente.
        returnHarpoonCoroutine = StartCoroutine(ReturnFreeHarpoon());
    }

    private IEnumerator ReturnFreeHarpoon()
    {
        yield return new WaitForSeconds(freeHarpoonReturnTime);

        returnHarpoonCoroutine = null;
        RetrieveHarpoon();
    }

    private void ShowHarpoon(Vector2 endPoint)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, endPoint);
    }

    private void UpdateAttachedHarpoonLine()
    {
        if (harpoonedEnemy == null || lineRenderer == null)
        {
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, transform.position);
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
}