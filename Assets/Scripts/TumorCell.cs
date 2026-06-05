using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TumorCell : MonoBehaviour
{
    [Header("Estado serious game")]
    [SerializeField] private TumorCellState initialState = TumorCellState.Senescent;
    [SerializeField] private bool canBeDestroyedByLinfo = true;

    [Header("Señales senescentes")]
    [SerializeField] private float signalInterval = 8f;
    [SerializeField] private int signalRange = 5;

    [Header("Multiplicación de despiertas")]
    [SerializeField] private float divisionDelayAfterWake = 6f;
    [SerializeField] private float divisionInterval = 10f;

    [Header("Contacto con LINFO")]
    [Tooltip("Tiempo de margen tras despertar una dormente. Durante este margen no daña aunque LINFO siga encima.")]
    [SerializeField] private float playerContactGraceTime = 1.75f;

    [Header("Aspecto")]
    [SerializeField] private Sprite senescentSprite;
    [SerializeField] private Sprite dormantSprite;
    [SerializeField] private Sprite awakeSprite;
    [SerializeField] private Color senescentColor = Color.white;
    [SerializeField] private Color dormantColor = new Color(0.62f, 0.50f, 0.66f, 0.8f);
    [SerializeField] private Color awakeColor = Color.white;
    [SerializeField] private Color destroyedColor = new Color(1f, 1f, 1f, 0f);

    [Header("Camuflaje dormente")]
    [Tooltip("Hace que las durmientes se vean más apagadas/camufladas mientras no han despertado.")]
    [SerializeField] private bool camouflageDormantCells = true;
    [Range(0.05f, 1f)]
    [SerializeField] private float dormantCamouflageAlpha = 0.42f;
    [SerializeField] private Color dormantCamouflageTint = new Color(0.55f, 0.48f, 0.58f, 1f);

    private TumorCellState currentState;
    private TumorGameManager manager;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Collider2D col;
    private Coroutine behaviourRoutine;
    private float lastWakeTime = -999f;
    private bool isDestroyed;

    public TumorCellState CurrentState => currentState;
    public Vector3Int GridPosition { get; private set; }
    public bool IsDestroyed => isDestroyed;

    protected virtual void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        currentState = initialState;
        ConfigurePhysics();
        ApplyVisualState();
    }

    protected virtual void Start()
    {
        RegisterInManager();
        RestartBehaviourRoutine();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && manager != null)
        {
            RestartBehaviourRoutine();
        }
    }

    private void OnDisable()
    {
        if (behaviourRoutine != null)
        {
            StopCoroutine(behaviourRoutine);
            behaviourRoutine = null;
        }
    }

    private void OnValidate()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (!Application.isPlaying)
        {
            currentState = initialState;
        }

        ApplyVisualState();
    }

    private void OnDestroy()
    {
        if (manager != null && !manager.IsShuttingDown)
        {
            manager.UnregisterCell(this);
        }
    }

    public void ConfigureRuntimeState(TumorCellState newState, TumorGameManager owner = null)
    {
        if (owner != null)
        {
            manager = owner;
        }

        currentState = newState;
        if (newState == TumorCellState.Awake)
        {
            lastWakeTime = Time.time;
        }

        ConfigurePhysics();
        ApplyVisualState();
        RestartBehaviourRoutine();
    }

    public void RefreshGridPosition()
    {
        if (manager == null)
        {
            manager = TumorGameManager.Instance;
        }

        GridPosition = manager != null
            ? manager.WorldToCell(transform.position)
            : Vector3Int.RoundToInt(transform.position);
    }

    public void WakeUp(string reason)
    {
        if (isDestroyed || currentState == TumorCellState.Awake)
        {
            return;
        }

        if (currentState != TumorCellState.Dormant)
        {
            return;
        }

        currentState = TumorCellState.Awake;
        lastWakeTime = Time.time;
        Debug.Log($"[Tumor] Célula dormente despertada: {reason}. LINFO tiene margen para apartarse.");
        manager?.NotifyDormantCellAwakened(this, reason);

        ApplyVisualState();
        RestartBehaviourRoutine();
        manager?.NotifyCellStateChanged(this);
    }

    public void ReceiveLinfoAttack()
    {
        if (isDestroyed || !canBeDestroyedByLinfo)
        {
            return;
        }

        DestroyCell(true);
    }

    public void DestroyCell(bool killedByLinfo)
    {
        if (isDestroyed)
        {
            return;
        }

        isDestroyed = true;

        if (behaviourRoutine != null)
        {
            StopCoroutine(behaviourRoutine);
            behaviourRoutine = null;
        }

        if (killedByLinfo)
        {
            LinfoSoundPlayer.Play("Cell_destroy_pop", 0.9f);
        }

        manager?.NotifyCellDestroyed(this, killedByLinfo);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = destroyedColor;
        }

        if (col != null)
        {
            col.enabled = false;
        }

        Destroy(gameObject, 0.05f);
    }

    private void RegisterInManager()
    {
        if (manager == null)
        {
            manager = TumorGameManager.Instance != null
                ? TumorGameManager.Instance
                : TumorGameManager.CreateRuntimeManager();
        }

        if (manager == null || manager.IsShuttingDown)
        {
            return;
        }

        RefreshGridPosition();
        manager.RegisterCell(this);
    }

    private void RestartBehaviourRoutine()
    {
        if (!Application.isPlaying || isDestroyed || !isActiveAndEnabled)
        {
            return;
        }

        if (behaviourRoutine != null)
        {
            StopCoroutine(behaviourRoutine);
        }

        if (currentState == TumorCellState.Senescent)
        {
            behaviourRoutine = StartCoroutine(SenescentSignalRoutine());
        }
        else if (currentState == TumorCellState.Awake)
        {
            behaviourRoutine = StartCoroutine(AwakeDivisionRoutine());
        }
        else
        {
            behaviourRoutine = null;
        }
    }

    private IEnumerator SenescentSignalRoutine()
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(0.6f, 1.4f));

        while (!isDestroyed && currentState == TumorCellState.Senescent)
        {
            manager?.EmitSenescentSignal(this, signalRange);
            yield return new WaitForSeconds(signalInterval);
        }
    }

    private IEnumerator AwakeDivisionRoutine()
    {
        yield return new WaitForSeconds(divisionDelayAfterWake);

        while (!isDestroyed && currentState == TumorCellState.Awake)
        {
            manager?.TryMultiplyAwakeCell(this);
            yield return new WaitForSeconds(divisionInterval);
        }
    }

    private void ConfigurePhysics()
    {
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void ApplyVisualState()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Sprite targetSprite = null;
        Color targetColor = Color.white;

        switch (currentState)
        {
            case TumorCellState.Senescent:
                targetSprite = senescentSprite;
                targetColor = senescentColor;
                break;

            case TumorCellState.Dormant:
                targetSprite = dormantSprite;
                targetColor = dormantColor;

                if (camouflageDormantCells)
                {
                    targetColor = new Color(
                        targetColor.r * dormantCamouflageTint.r,
                        targetColor.g * dormantCamouflageTint.g,
                        targetColor.b * dormantCamouflageTint.b,
                        targetColor.a * dormantCamouflageAlpha
                    );
                }
                break;

            case TumorCellState.Awake:
                targetSprite = awakeSprite;
                targetColor = awakeColor;
                break;

            default:
                targetSprite = senescentSprite != null ? senescentSprite : spriteRenderer.sprite;
                targetColor = Color.white;
                break;
        }

        if (targetSprite != null)
        {
            spriteRenderer.sprite = targetSprite;
        }

        spriteRenderer.color = targetColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandlePlayerContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandlePlayerContact(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandlePlayerContact(collision.collider);
    }

    private void HandlePlayerContact(Collider2D other)
    {
        if (isDestroyed || !other.CompareTag("Player"))
        {
            return;
        }

        if (currentState == TumorCellState.Dormant)
        {
            WakeUp("contacto directo con LINFO");
            return;
        }

        if (currentState == TumorCellState.Awake)
        {
            if (Time.time - lastWakeTime < playerContactGraceTime)
            {
                return;
            }

            PlayerDeath death = other.GetComponentInParent<PlayerDeath>();
            if (death != null)
            {
                death.Die();
            }
        }
    }
}
