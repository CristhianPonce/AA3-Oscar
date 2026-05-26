using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Configuraci�n Base de Enemigo")]
    [SerializeField] protected float normalSpeed = 2f;
    [SerializeField] protected float ghostSpeed = 1f;
    [SerializeField] protected float ghostModeDuration = 5f;
    [SerializeField] protected LayerMask groundLayer;

    protected Rigidbody2D rb;
    protected Collider2D col;
    protected Transform player;

    protected bool isGhost = false;
    protected int inflateStage = 0;
    protected const int maxInflateStage = 4;
    protected float inflateTimer = 0f;

    protected Vector2 currentDirection = Vector2.right;

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        player = GameObject.FindGameObjectWithTag("Player").transform;

        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Bucle para decidir aleatoriamente cu�ndo convertirse en fantasma
        InvokeRepeating(nameof(ToggleGhostMode), Random.Range(10f, 15f), 15f);
    }

    protected virtual void Update()
    {
        // Manejar el desinflado autom�tico con el tiempo
        if (inflateStage > 0)
        {
            inflateTimer += Time.deltaTime;
            if (inflateTimer > 1f)
            {
                Deflate();
            }
            return; // Paralizado mientras est� siendo inflado
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

    // IA b�sica: Caminar en l�nea recta por los t�neles y rebotar al tocar tierra
    protected virtual void MoveInTunnels()
    {
        rb.linearVelocity = currentDirection * normalSpeed;

        // Raycast para ver si hay un muro o tierra adelante
        RaycastHit2D hit = Physics2D.Raycast(transform.position, currentDirection, 0.6f, groundLayer);
        if (hit.collider != null)
        {
            // Cambiar a una direcci�n aleatoria en cruz
            Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            currentDirection = directions[Random.Range(0, directions.Length)];
        }
    }

    // IA Fantasma: Flotar directamente hacia el jugador ignorando paredes
    protected virtual void MoveAsGhost()
    {
        if (player == null) return;
        Vector2 directionToPlayer = (player.position - transform.position).normalized;
        rb.linearVelocity = directionToPlayer * ghostSpeed;
    }

    public void GetInflated()
    {
        inflateTimer = 0f; // Resetear tiempo de desinflado
        inflateStage++;

        // Efecto visual de inflado aumentando la escala del sprite
        transform.localScale = Vector3.one * (1f + (inflateStage * 0.25f));

        rb.linearVelocity = Vector2.zero; // Queda completamente inm�vil

        if (inflateStage >= maxInflateStage)
        {
            Die();
        }
    }

    protected void Deflate()
    {
        inflateStage--;
        transform.localScale = Vector3.one * (1f + (inflateStage * 0.25f));
        inflateTimer = 0f;
    }

    protected virtual void ToggleGhostMode()
    {
        isGhost = !isGhost;

        if (isGhost)
        {
            col.isTrigger = true; // Ignora colisiones f�sicas con el mapa para atravesar tierra
            GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.6f); // Efecto transparente
            Invoke(nameof(ToggleGhostMode), ghostModeDuration); // Volver a la normalidad tras X segundos
        }
        else
        {
            col.isTrigger = false;
            GetComponent<SpriteRenderer>().color = Color.white;
        }
    }

    protected virtual void Die()
    {
        CancelInvoke();
        Destroy(gameObject);
    }
}