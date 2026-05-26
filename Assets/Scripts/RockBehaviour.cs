using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RockBehavior : MonoBehaviour
{
    [Header("Configuración de Caída")]
    [SerializeField] private LayerMask groundLayer; // Capa del Tilemap de tierra
    [SerializeField] private float fallSpeed = 8f;
    [SerializeField] private float delayBeforeFall = 0.8f;

    private Rigidbody2D rb;
    private bool isFalling = false;
    private bool isTriggered = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Configuración moderna: Aseguramos que sea Dynamic pero congelamos su posición al inicio
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f; // Evitamos que la gravedad de Unity actúe antes de tiempo
        FreezeRock();
    }

    void Update()
    {
        // Si ya está cayendo o esperando para caer, no comprobamos nada más
        if (isFalling || isTriggered) return;

        // Lanzamos un rayo corto hacia abajo (ajusta el 0.6f según el tamaño de tu sprite)
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 0.6f, groundLayer);

        // Si no hay tierra debajo, la roca empieza a activarse
        if (hit.collider == null)
        {
            StartCoroutine(TriggerFallRoutine());
        }
    }

    private IEnumerator TriggerFallRoutine()
    {
        isTriggered = true;

        // El clásico delay de Dig Dug donde la roca tiembla antes de caer
        float elapsed = 0f;
        Vector3 originalPos = transform.position;
        while (elapsed < delayBeforeFall)
        {
            transform.position = originalPos + (Vector3)Random.insideUnitCircle * 0.05f;
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPos; // Restaurar posición tras el temblor

        // Cambiamos el estado para empezar a caer
        isFalling = true;
        UnfreezeRock();
    }

    private void FixedUpdate()
    {
        if (isFalling)
        {
            // La forma moderna y recomendada para mover cuerpos físicos de forma controlada
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
        if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.CompareTag("Player"))
        {
            // Aquí puedes llamar al método de daño/muerte del objetivo
            Destroy(collision.gameObject);
        }
    }

    private void StopFalling()
    {
        isFalling = false;
        isTriggered = false;
        FreezeRock();

        // Opcional: En Dig Dug las rocas se rompen tras caer para no bloquear el mapa para siempre
        Destroy(gameObject, 0.2f);
    }

    private void FreezeRock()
    {
        // Congelamos tanto el movimiento como la rotación para que no se deslice por físicas
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private void UnfreezeRock()
    {
        // Al caer, solo permitimos el movimiento en el eje Y y congelamos la rotación
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
    }
}