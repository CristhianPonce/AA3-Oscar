using System.Collections;
using UnityEngine;

public class EnemyFygar : EnemyBase
{
    [Header("Ajustes de Fygar (Drag�n)")]
    [SerializeField] private GameObject firePrefab; // Asigna un sprite/trigger de fuego
    [SerializeField] private float attackCooldown = 4f;

    private bool isAttacking = false;

    protected override void Start()
    {
        base.Start();
        normalSpeed = 1.8f; // El drag�n suele ser un poco m�s lento
        StartCoroutine(AttackRoutine());
    }

    protected override void Update()
    {
        if (isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            return; // No se mueve mientras escupe fuego
        }

        base.Update();
    }

    private IEnumerator AttackRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(attackCooldown, attackCooldown + 3f));

            // Solo ataca si est� en modo normal (en t�neles) y no est� siendo inflado
            if (!isGhost && inflateStage == 0 && player != null)
            {
                isAttacking = true;
                rb.linearVelocity = Vector2.zero;

                // En Dig Dug, Fygar solo escupe fuego a la izquierda o derecha
                // Miramos en qu� direcci�n horizontal est� el jugador para girar hacia �l antes de atacar
                currentDirection = (player.position.x > transform.position.x) ? Vector2.right : Vector2.left;

                // Aqu� activar�as tu animaci�n de cargar fuego
                yield return new WaitForSeconds(0.5f);

                // Instanciar el fuego
                if (firePrefab != null)
                {
                    GameObject fire = Instantiate(firePrefab, transform.position + (Vector3)currentDirection * 0.8f, Quaternion.identity);
                    // Orientar el fuego en la direcci�n correcta
                    fire.transform.right = currentDirection;
                    Destroy(fire, 0.6f); // El fuego dura poco en pantalla
                }

                yield return new WaitForSeconds(0.5f);
                isAttacking = false;
            }
        }
    }
}