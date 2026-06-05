using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RockBehavior : MonoBehaviour
{
    [Header("Configuracin de Cada")]
    [SerializeField] private LayerMask groundLayer; // Capa del Tilemap de tierra
    [SerializeField] private float fallSpeed = 6f;
    [SerializeField] private float delayBeforeFall = 1.5f;

    private Rigidbody2D rb;
    private bool isFalling = false;
    private bool isTriggered = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Configuracin moderna: Aseguramos que sea Dynamic pero congelamos su posicin al inicio
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f; // Evitamos que la gravedad de Unity acte antes de tiempo
        FreezeRock();
    }

    void Update()
    {
        // Si ya est cayendo o esperando para caer, no comprobamos nada ms
        if (isFalling || isTriggered) return;

        // Lanzamos un rayo corto hacia abajo (ajusta el 0.6f segn el tamao de tu sprite)
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 0.6f, groundLayer);

        // Si no hay tierra debajo, la roca empieza a activarse
        if (hit.collider != null)
        {
            // 2. Si ha tocado algo, comprobamos si ese algo es el suelo
            if (!hit.collider.CompareTag("Ground"))
                StartCoroutine(TriggerFallRoutine());
        }
        else
        {
            Debug.Log("[Rock] Sin soporte: la roca empieza a caer.");
            StartCoroutine(TriggerFallRoutine());
        }
    }

    private IEnumerator TriggerFallRoutine()
    {
        isTriggered = true;

        // El clsico delay de Dig Dug donde la roca tiembla antes de caer
        float elapsed = 0f;
        Vector3 originalPos = transform.position;
        while (elapsed < delayBeforeFall)
        {
            transform.position = originalPos + (Vector3)UnityEngine.Random.insideUnitCircle * 0.05f;
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPos; // Restaurar posicin tras el temblor

        // Cambiamos el estado para empezar a caer
        isFalling = true;
        UnfreezeRock();
    }

    private void FixedUpdate()
    {
        if (isFalling)
        {
            // La forma moderna y recomendada para mover cuerpos fsicos de forma controlada
            rb.MovePosition(rb.position + Vector2.down * fallSpeed * Time.fixedDeltaTime);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isFalling) return;

        // Si choca con el suelo (Tilemap con collider) u otra roca ya asentada
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Rock"))
        {
            StopFalling();
        }
        // Si aplasta al jugador o a un enemigo
        if (//collision.gameObject.CompareTag("Enemy")
            collision.gameObject.CompareTag("Player"))
        {

            TryKillPlayer(collision.collider);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        //if (!isFalling) return;

        //// Si choca con el suelo (Tilemap con collider) u otra roca ya asentada
        //if (other.gameObject.CompareTag("Ground") || other.gameObject.CompareTag("Rock"))
        //{
        //    StopFalling();
        //}
        //// Si aplasta al jugador o a un enemigo
        //if (//collision.gameObject.CompareTag("Enemy")
        //    other.gameObject.CompareTag("Player"))
        //{

        //    TryKillPlayer(other.collider);
        //}
    }

    private void TryKillPlayer(Collider2D other)
    {
        PlayerDeath playerDeath = other.GetComponentInParent<PlayerDeath>();

        if (playerDeath != null)
        {
            playerDeath.Die();
        }
    }

    private void StopFalling()
    {
        Debug.Log("[Rock] La roca ha dejado de caer.");
        isFalling = false;
        isTriggered = false;
        FreezeRock();

        // Opcional: En Dig Dug las rocas se rompen tras caer para no bloquear el mapa para siempre
        Destroy(gameObject, 0.5f);
    }

    private void FreezeRock()
    {
        // Congelamos tanto el movimiento como la rotacin para que no se deslice por fsicas
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private void UnfreezeRock()
    {
        // Al caer, solo permitimos el movimiento en el eje Y y congelamos la rotacin
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
    }
}