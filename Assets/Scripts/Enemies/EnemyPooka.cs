using UnityEngine;

/// <summary>
/// Enemigo Pooka del Dig Dug.
/// - Se mueve por túneles usando el sistema de grid de EnemyBase.
/// - Periódicamente se convierte en fantasma y atraviesa paredes para perseguir al jugador.
/// - Al recuperar la forma normal vuelve al movimiento por túneles.
/// </summary>
public class EnemyPooka : EnemyBase
{
    [Header("Pooka - Configuración específica")]
    [SerializeField] private float pookaPatrolSpeed = 2.5f;
    [SerializeField] private float pookaGhostSpeed = 2f;

    protected override void Start()
    {
        // Asignamos las velocidades específicas del Pooka antes de llamar a base.Start()
        normalSpeed = pookaPatrolSpeed;
        ghostSpeed  = pookaGhostSpeed;

        base.Start();
    }

    // Si en el futuro el Pooka necesita comportamientos propios de IA
    // (p.ej. patrullar en lugar de perseguir), puedes sobreescribir
    // UpdateDesiredDirection() aquí sin tocar EnemyBase.
}
