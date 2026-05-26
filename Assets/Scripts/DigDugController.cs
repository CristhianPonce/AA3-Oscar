using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class DigDugController : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Tilemap groundTilemap;

    [Header("Configuración del Arpón")]
    [SerializeField] private float attackRange = 4f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LineRenderer lineRenderer; // Asigna un LineRenderer en el Inspector

    private Vector2 moveDirection = Vector2.right; // Guarda la última dirección al mirar
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
                // Disparo / Inflar con Barra Espaciadora
                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    ShootHarpoon();
                    return;
                }

                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX = 1f;
                else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX = -1f;

                if (moveX == 0)
                {
                    if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveY = 1f;
                    else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveY = -1f;
                }
            }

            if (moveX != 0) currentInputDirection = new Vector2(moveX, 0);
            else if (moveY != 0) currentInputDirection = new Vector2(0, moveY);
            else currentInputDirection = Vector2.zero;

            if (currentInputDirection != Vector2.zero)
            {
                moveDirection = currentInputDirection; // Actualizar dirección de mirada
                targetPosition = (Vector2)transform.position + moveDirection;
                isMoving = true;
            }
        }
    }

    void FixedUpdate()
    {
        if (isMoving && !isAttacking)
        {
            Vector2 newPos = Vector2.MoveTowards(rb.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);
            TryDig();

            if (Vector2.Distance(rb.position, targetPosition) < 0.05f)
            {
                rb.position = targetPosition;
                isMoving = false;
            }
        }
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

        // Lanzar Raycast para detectar enemigos
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

        Invoke(nameof(ResetAttack), 0.3f); // El cable desaparece rápido si no se sigue inflando
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
            // Si el jugador deja presionado espacio, re-evaluamos el disparo (para seguir inflando)
            ShootHarpoon();
        }
    }
}