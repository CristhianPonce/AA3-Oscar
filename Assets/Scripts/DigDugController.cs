using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class DigDugController : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private Tilemap groundTilemap;
    // NUEVO: Capa de los obstáculos indestructibles
    [SerializeField] private LayerMask hardGroundLayer;

    [Header("Configuración del Arpón")]
    [SerializeField] private float attackRange = 4f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LineRenderer lineRenderer;

    private Vector2 moveDirection = Vector2.right;
    private Vector2 currentInputDirection;
    private Rigidbody2D rb;
    private Vector2 targetPosition;
    private bool isMoving;
    private bool isAttacking = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        targetPosition = transform.position;
        if (lineRenderer) lineRenderer.enabled = false;
    }

    void Update()
    {
        if (isAttacking) return; // No moverse mientras ataca o infla

        if (!isMoving)
        {
            float moveX = 0;
            float moveY = 0;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    ShootHarpoon();
                    return;
                }

                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX = groundTilemap.cellSize.x;
                else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX = -groundTilemap.cellSize.x;

                if (moveX == 0)
                {
                    if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveY = groundTilemap.cellSize.y;
                    else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveY = -groundTilemap.cellSize.y;
                }
            }

            if (moveX != 0) currentInputDirection = new Vector2(moveX, 0);
            else if (moveY != 0) currentInputDirection = new Vector2(0, moveY);
            else currentInputDirection = Vector2.zero;

            if (currentInputDirection != Vector2.zero)
            {
                moveDirection = currentInputDirection.normalized;
                Vector2 potentialTarget = (Vector2)transform.position + currentInputDirection;

                if (CanMove(potentialTarget))
                {
                    targetPosition = potentialTarget;
                    isMoving = true; // Solo se bloquea el teclado si el movimiento es VÁLIDO
                }
                else
                {
                    // CORRECCIÓN: Si el camino está bloqueado, cancelamos la dirección
                    // y dejamos 'isMoving' en false para que el teclado siga activo el próximo frame
                    currentInputDirection = Vector2.zero;
                    isMoving = false;
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (isMoving && !isAttacking)
        {
            TryDig();
            Vector2 newPos = Vector2.MoveTowards(rb.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);

            if (Vector2.Distance(rb.position, targetPosition) < 0.05f)
            {
                rb.position = targetPosition;
                isMoving = false;
            }
        }
    }

    // NUEVA FUNCIÓN: Comprueba si hay HardGround en la posición de destino
    private bool CanMove(Vector2 targetPos)
    {
        // Comprobamos si hay algún collider de la capa HardGround en el punto exacto de destino
        Collider2D hit = Physics2D.OverlapPoint(targetPos, hardGroundLayer);

        // Si hit es null, significa que el camino está despejado
        return hit == null;
    }

    void TryDig()
    {
        if (groundTilemap == null) return;
        Vector3Int tilePosition = groundTilemap.WorldToCell((Vector3)targetPosition);
        if (groundTilemap.HasTile(tilePosition)) groundTilemap.SetTile(tilePosition, null);
    }

    void ShootHarpoon()
    {
        isAttacking = true;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, moveDirection, attackRange, enemyLayer);

        if (lineRenderer)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, transform.position);
            Vector3 endPos = hit.collider != null ? (Vector3)hit.point : transform.position + (Vector3)moveDirection * attackRange;
            lineRenderer.SetPosition(1, endPos);
        }

        if (hit.collider != null)
        {
            EnemyBase enemy = hit.collider.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.GetInflated();
            }
        }

        Invoke(nameof(ResetAttack), 0.3f);
    }

    void ResetAttack()
    {
        if (!Keyboard.current.spaceKey.isPressed)
        {
            if (lineRenderer) lineRenderer.enabled = false;
            isAttacking = false;
        }
        else
        {
            ShootHarpoon();
        }
    }
}