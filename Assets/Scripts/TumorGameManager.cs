using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

[DefaultExecutionOrder(-900)]
public class TumorGameManager : MonoBehaviour
{
    public static TumorGameManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private Tilemap innocentTumorTilemap;
    [SerializeField] private TumorCell tumorCellPrefab;
    [SerializeField] private Transform generatedCellsParent;

    [Header("Condiciones de partida")]
    [SerializeField] private int maxAwakeCellsBeforeDefeat = 25;
    [SerializeField] private float endStateDelay = 1.2f;

    [Header("Vidas de LINFO")]
    [SerializeField] private int maxPlayerLives = 3;
    [SerializeField] private float playerRespawnInvulnerabilityTime = 1.8f;

    [Header("Spawn aleatorio de senescentes")]
    [SerializeField] private bool randomizeSenescentCellsOnStart = true;
    [SerializeField] private int randomSenescentCellsToSpawn = 5;
    [SerializeField] private bool useExistingSenescentCountIfHigher = true;
    [SerializeField] private int minDistanceFromPlayerForSenescentSpawn = 4;
    [SerializeField] private bool removeTileUnderSpawnedSenescentCells = true;

    [Header("Propagación")]
    [SerializeField] private int maxConversionSearchRadius = 2;
    [SerializeField] private int dormantsAwakenedPerSignal = 1;
    [SerializeField] private bool signalsRequireTumorPath = false;
    [SerializeField] private bool convertOnlyInnocentTumorTiles = true;

    [Header("Auto setup para prototipo")]
    [SerializeField] private bool autoSeedDormantCellsWhenMissing = true;
    [SerializeField] private int autoDormantCellsToSeed = 5;
    [SerializeField] private int minDistanceFromPlayerForAutoSeeds = 4;
    [SerializeField] private bool removeTileUnderDormantCells = false;
    [SerializeField] private bool removeTileUnderDormantWhenAwakenedBySignal = true;

    [Header("UI automática")]
    [SerializeField] private bool autoCreateAwakeProgressUI = true;
    [SerializeField] private Vector2 awakeBarAnchoredPosition = new Vector2(0f, -32f);
    [SerializeField] private Vector2 awakeBarSize = new Vector2(420f, 28f);
    [SerializeField] private Color awakeBarBackgroundColor = new Color(0.08f, 0.02f, 0.07f, 0.85f);
    [SerializeField] private Color awakeBarFillColor = new Color(1f, 0.12f, 0.25f, 0.95f);
    [SerializeField] private Color awakeBarSafeFillColor = new Color(0.25f, 0.8f, 1f, 0.95f);

    [Header("Pantalla final automática")]
    [SerializeField] private bool showEndScreenInsteadOfAutoReload = true;
    [SerializeField] private Color endScreenBackgroundColor = new Color(0.02f, 0.02f, 0.05f, 0.88f);
    [SerializeField] private Color victoryTitleColor = new Color(0.35f, 1f, 0.65f, 1f);
    [SerializeField] private Color defeatTitleColor = new Color(1f, 0.25f, 0.28f, 1f);
    [SerializeField] private Color buttonColor = new Color(0.12f, 0.38f, 0.62f, 0.95f);
    [SerializeField] private Color retryButtonColor = new Color(0.52f, 0.24f, 0.65f, 0.95f);

    [Header("Feedback")]
    [SerializeField] private bool showOnGuiDebugHud = false;
    [SerializeField] private bool showSignalLines = true;
    [SerializeField] private float signalLineDuration = 0.25f;
    [SerializeField] private float signalLineWidth = 0.05f;
    [SerializeField] private Color signalLineColor = new Color(1f, 0.92f, 0.2f, 0.8f);

    private readonly Dictionary<Vector3Int, TumorCell> cellsByGridPosition = new Dictionary<Vector3Int, TumorCell>();
    private readonly HashSet<TumorCell> cells = new HashSet<TumorCell>();
    private readonly Vector3Int[] cardinalDirections =
    {
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down
    };

    private Material signalLineMaterial;
    private bool sceneInitialized;
    private bool endingTriggered;
    private bool isShuttingDown;
    private static bool applicationQuitting;
    private int initialDangerousCellCount;
    private int currentPlayerLives;

    private Slider awakeCellsSlider;
    private Image awakeCellsSliderFill;
    private TextMeshProUGUI awakeCellsLabel;
    private TextMeshProUGUI livesLabel;
    private TextMeshProUGUI objectiveLabel;
    private GameObject endScreenRoot;
    private bool isEndScreenVisible;
    private bool endScreenVictory;

    public int SenescentCount { get; private set; }
    public int DormantCount { get; private set; }
    public int AwakeCount { get; private set; }
    public int DangerousCellCount => SenescentCount + DormantCount + AwakeCount;
    public int MaxAwakeCellsBeforeDefeat => maxAwakeCellsBeforeDefeat;
    public int PlayerLives => currentPlayerLives;
    public int MaxPlayerLives => maxPlayerLives;
    public bool IsShuttingDown => isShuttingDown || applicationQuitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateAfterSceneLoad()
    {
        applicationQuitting = false;
        CreateRuntimeManager();
    }

    public static TumorGameManager CreateRuntimeManager()
    {
        if (applicationQuitting || !Application.isPlaying)
        {
            return null;
        }

        if (Instance != null)
        {
            return Instance;
        }

        TumorGameManager existing = FindFirstObjectByType<TumorGameManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("Tumor Game Manager");
        return go.AddComponent<TumorGameManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentPlayerLives = Mathf.Max(1, maxPlayerLives);
    }

    private void Start()
    {
        Time.timeScale = 1f;
        InitializeSceneIfNeeded();
    }

    private void Update()
    {
        if (IsShuttingDown)
        {
            return;
        }

        UpdateRuntimeUI();
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
        isShuttingDown = true;
    }

    private void OnDestroy()
    {
        isShuttingDown = true;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void InitializeSceneIfNeeded()
    {
        if (sceneInitialized)
        {
            return;
        }

        sceneInitialized = true;
        FindReferencesIfMissing();
        DiscoverExistingTumorCells();
        RandomizeSenescentCellsIfNeeded();
        AutoSeedDormantCellsIfNeeded();
        RecalculateCounters();
        initialDangerousCellCount = DangerousCellCount;

        if (autoCreateAwakeProgressUI)
        {
            CreateRuntimeUIIfNeeded();
        }

        UpdateRuntimeUI();

        Debug.Log($"[Tumor] Nivel listo. Senescentes: {SenescentCount}, Dormentes: {DormantCount}, Despiertas: {AwakeCount}. Límite de derrota: {maxAwakeCellsBeforeDefeat}. Vidas: {currentPlayerLives}/{maxPlayerLives}.");
    }

    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        if (innocentTumorTilemap != null)
        {
            return innocentTumorTilemap.WorldToCell(worldPosition);
        }

        return Vector3Int.RoundToInt(worldPosition);
    }

    public Vector3 CellToWorldCenter(Vector3Int cellPosition)
    {
        if (innocentTumorTilemap != null)
        {
            return innocentTumorTilemap.GetCellCenterWorld(cellPosition);
        }

        return cellPosition;
    }

    public void RegisterCell(TumorCell cell)
    {
        if (cell == null || cell.IsDestroyed)
        {
            return;
        }

        InitializeReferencesOnly();

        cells.Add(cell);
        cell.RefreshGridPosition();

        if (!cellsByGridPosition.TryGetValue(cell.GridPosition, out TumorCell existing) || existing == null || existing == cell)
        {
            cellsByGridPosition[cell.GridPosition] = cell;
        }

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            cell.gameObject.layer = enemyLayer;
        }

        if (tumorCellPrefab == null)
        {
            tumorCellPrefab = cell;
        }

        RecalculateCounters();
        if (!IsShuttingDown)
        {
            UpdateRuntimeUI();
        }
    }

    public void UnregisterCell(TumorCell cell)
    {
        if (cell == null)
        {
            return;
        }

        cells.Remove(cell);

        if (cellsByGridPosition.TryGetValue(cell.GridPosition, out TumorCell registered) && registered == cell)
        {
            cellsByGridPosition.Remove(cell.GridPosition);
        }

        RecalculateCounters();
        if (!IsShuttingDown)
        {
            UpdateRuntimeUI();
        }
    }

    public void NotifyCellStateChanged(TumorCell cell)
    {
        if (cell == null)
        {
            return;
        }

        RegisterCell(cell);
        RecalculateCounters();
        if (!IsShuttingDown)
        {
            UpdateRuntimeUI();
        }
        CheckDefeatCondition();
    }

    public void NotifyCellDestroyed(TumorCell cell, bool killedByLinfo)
    {
        if (cell == null)
        {
            return;
        }

        TumorCellState destroyedState = cell.CurrentState;

        UnregisterCell(cell);
        RecalculateCounters();
        if (!IsShuttingDown)
        {
            UpdateRuntimeUI();
        }

        if (killedByLinfo)
        {
            Debug.Log($"[Tumor] LINFO ha eliminado una célula {destroyedState}.");
        }

        CheckVictoryCondition();
    }

    public void OnInnocentCellDug(Vector3Int cellPosition)
    {
        // Hook preparado para puntos, partículas o feedback extra al destruir tejido tumoral inocuo.
    }

    public void RemoveInnocentTileUnderCell(TumorCell cell)
    {
        if (cell == null || IsShuttingDown)
        {
            return;
        }

        RemoveInnocentTileAt(cell.GridPosition);
    }

    public void NotifyDormantCellAwakened(TumorCell cell, string reason)
    {
        if (cell == null || IsShuttingDown)
        {
            return;
        }

        bool awakenedBySignal = !string.IsNullOrEmpty(reason) && reason.ToLowerInvariant().Contains("señal");
        if (awakenedBySignal && removeTileUnderDormantWhenAwakenedBySignal)
        {
            RemoveInnocentTileAt(cell.GridPosition);
        }

        LinfoSoundPlayer.Play("Cell_dormant_wake_up", 0.9f);
    }

    public void OnPlayerHit(PlayerDeath playerDeath)
    {
        if (endingTriggered || playerDeath == null)
        {
            return;
        }

        currentPlayerLives = Mathf.Max(0, currentPlayerLives - 1);
        LinfoSoundPlayer.Play("LINFO_damage_warning", 1f);
        if (!IsShuttingDown)
        {
            UpdateRuntimeUI();
        }

        if (currentPlayerLives <= 0)
        {
            TriggerDefeat("[Tumor] Derrota: LINFO se ha quedado sin vidas.");
            return;
        }

        Debug.Log($"[Tumor] LINFO ha perdido una vida. Vidas restantes: {currentPlayerLives}/{maxPlayerLives}.");
        playerDeath.Respawn(playerRespawnInvulnerabilityTime);
    }

    public void EmitSenescentSignal(TumorCell senescentCell, int signalRange)
    {
        if (senescentCell == null || senescentCell.IsDestroyed || endingTriggered)
        {
            return;
        }

        List<TumorCell> candidates = new List<TumorCell>();
        foreach (TumorCell cell in cells)
        {
            if (cell == null || cell.IsDestroyed || cell.CurrentState != TumorCellState.Dormant)
            {
                continue;
            }

            int distance = ManhattanDistance(senescentCell.GridPosition, cell.GridPosition);
            if (distance > signalRange)
            {
                continue;
            }

            if (signalsRequireTumorPath && !HasSignalPathThroughTumor(senescentCell.GridPosition, cell.GridPosition, signalRange))
            {
                continue;
            }

            candidates.Add(cell);
        }

        candidates.Sort((a, b) => ManhattanDistance(senescentCell.GridPosition, a.GridPosition).CompareTo(ManhattanDistance(senescentCell.GridPosition, b.GridPosition)));

        int awakened = 0;
        foreach (TumorCell dormant in candidates)
        {
            if (awakened >= dormantsAwakenedPerSignal)
            {
                break;
            }

            if (showSignalLines)
            {
                DrawSignalLine(senescentCell.transform.position, dormant.transform.position);
            }

            LinfoSoundPlayer.Play("Cell_senescent_signal_pulse", 0.65f);
            dormant.WakeUp("señal senescente");
            awakened++;
        }
    }

    public void TryMultiplyAwakeCell(TumorCell source)
    {
        if (source == null || source.IsDestroyed || source.CurrentState != TumorCellState.Awake || endingTriggered)
        {
            return;
        }

        if (AwakeCount >= maxAwakeCellsBeforeDefeat)
        {
            CheckDefeatCondition();
            return;
        }

        if (!TryFindConvertibleInnocentCell(source.GridPosition, out Vector3Int targetCell))
        {
            Debug.Log("[Tumor] Una célula despierta intenta multiplicarse, pero no encuentra células inocuas cercanas.");
            return;
        }

        ConvertInnocentCellIntoAwake(targetCell, source.transform.position);
        CheckDefeatCondition();
    }

    private void ConvertInnocentCellIntoAwake(Vector3Int targetCell, Vector3 sourcePosition)
    {
        RemoveInnocentTileAt(targetCell);
        TumorCell newCell = CreateCell(targetCell, TumorCellState.Awake, "Awake Cell");

        if (newCell != null)
        {
            Debug.Log($"[Tumor] Célula despierta multiplicada en {targetCell}.");
            LinfoSoundPlayer.Play("Cell_awake_multiply", 0.85f);
            if (showSignalLines)
            {
                DrawSignalLine(sourcePosition, newCell.transform.position, new Color(1f, 0.15f, 0.15f, 0.8f));
            }
        }
    }

    private TumorCell CreateCell(Vector3Int gridPosition, TumorCellState state, string baseName)
    {
        InitializeReferencesOnly();

        TumorCell newCell = null;
        Vector3 worldPosition = CellToWorldCenter(gridPosition);
        worldPosition.z = tumorCellPrefab != null ? tumorCellPrefab.transform.position.z : -5f;

        if (tumorCellPrefab != null)
        {
            TumorCell prefabToClone = tumorCellPrefab;
            newCell = Instantiate(prefabToClone, worldPosition, Quaternion.identity, generatedCellsParent);
            newCell.name = $"{baseName} ({gridPosition.x},{gridPosition.y})";
        }
        else
        {
            GameObject go = new GameObject($"{baseName} ({gridPosition.x},{gridPosition.y})");
            go.transform.position = worldPosition;
            if (generatedCellsParent != null)
            {
                go.transform.SetParent(generatedCellsParent);
            }

            CircleCollider2D circle = go.AddComponent<CircleCollider2D>();
            circle.radius = 0.35f;
            circle.isTrigger = true;
            newCell = go.AddComponent<TumorCell>();
        }

        newCell.ConfigureRuntimeState(state, this);
        newCell.RefreshGridPosition();
        RegisterCell(newCell);
        return newCell;
    }

    private void RandomizeSenescentCellsIfNeeded()
    {
        if (!randomizeSenescentCellsOnStart || innocentTumorTilemap == null)
        {
            return;
        }

        List<TumorCell> existingSenescentCells = new List<TumorCell>();
        foreach (TumorCell cell in new List<TumorCell>(cells))
        {
            if (cell != null && !cell.IsDestroyed && cell.CurrentState == TumorCellState.Senescent)
            {
                existingSenescentCells.Add(cell);
            }
        }

        if (existingSenescentCells.Count == 0 && tumorCellPrefab == null)
        {
            return;
        }

        int amount = Mathf.Max(0, randomSenescentCellsToSpawn);
        if (useExistingSenescentCountIfHigher)
        {
            amount = Mathf.Max(amount, existingSenescentCells.Count);
        }

        if (amount <= 0)
        {
            return;
        }

        List<Vector3Int> candidates = CollectFreeTumorTileCandidates(minDistanceFromPlayerForSenescentSpawn);
        if (candidates.Count == 0)
        {
            Debug.LogWarning("[Tumor] No se han encontrado tiles válidos para spawnear células senescentes.");
            return;
        }

        foreach (TumorCell cell in existingSenescentCells)
        {
            UnregisterCell(cell);
        }

        int spawned = 0;
        for (int i = 0; i < amount && candidates.Count > 0; i++)
        {
            int index = UnityEngine.Random.Range(0, candidates.Count);
            Vector3Int chosen = candidates[index];
            candidates.RemoveAt(index);

            if (removeTileUnderSpawnedSenescentCells)
            {
                RemoveInnocentTileAt(chosen);
            }

            if (i < existingSenescentCells.Count && existingSenescentCells[i] != null)
            {
                TumorCell reused = existingSenescentCells[i];
                Vector3 worldPosition = CellToWorldCenter(chosen);
                worldPosition.z = reused.transform.position.z;
                reused.transform.position = worldPosition;
                reused.ConfigureRuntimeState(TumorCellState.Senescent, this);
                reused.RefreshGridPosition();
                RegisterCell(reused);
            }
            else
            {
                CreateCell(chosen, TumorCellState.Senescent, "Senescent Cell");
            }

            spawned++;
        }

        for (int i = spawned; i < existingSenescentCells.Count; i++)
        {
            if (existingSenescentCells[i] != null)
            {
                Destroy(existingSenescentCells[i].gameObject);
            }
        }

        RecalculateCounters();
        Debug.Log($"[Tumor] Spawn aleatorio: creadas/reubicadas {spawned} células senescentes.");
    }

    private void AutoSeedDormantCellsIfNeeded()
    {
        if (!autoSeedDormantCellsWhenMissing || DormantCount > 0 || tumorCellPrefab == null || innocentTumorTilemap == null)
        {
            return;
        }

        List<Vector3Int> candidates = CollectFreeTumorTileCandidates(minDistanceFromPlayerForAutoSeeds);
        int amount = Mathf.Min(autoDormantCellsToSeed, candidates.Count);

        for (int i = 0; i < amount; i++)
        {
            int index = UnityEngine.Random.Range(0, candidates.Count);
            Vector3Int chosen = candidates[index];
            candidates.RemoveAt(index);

            if (removeTileUnderDormantCells)
            {
                RemoveInnocentTileAt(chosen);
            }

            CreateCell(chosen, TumorCellState.Dormant, "Dormant Cell");
        }

        RecalculateCounters();
        Debug.Log($"[Tumor] Auto setup: creadas {amount} células dormentes camufladas.");
    }

    private List<Vector3Int> CollectFreeTumorTileCandidates(int minDistanceFromPlayer)
    {
        List<Vector3Int> candidates = new List<Vector3Int>();
        if (innocentTumorTilemap == null)
        {
            return candidates;
        }

        BoundsInt bounds = innocentTumorTilemap.cellBounds;
        Transform player = FindPlayerTransform();
        Vector3Int playerCell = player != null ? WorldToCell(player.position) : Vector3Int.zero;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (!innocentTumorTilemap.HasTile(cellPosition) || cellsByGridPosition.ContainsKey(cellPosition))
            {
                continue;
            }

            if (player != null && ManhattanDistance(playerCell, cellPosition) < minDistanceFromPlayer)
            {
                continue;
            }

            candidates.Add(cellPosition);
        }

        return candidates;
    }

    private void RemoveInnocentTileAt(Vector3Int cellPosition)
    {
        if (innocentTumorTilemap != null && innocentTumorTilemap.HasTile(cellPosition))
        {
            innocentTumorTilemap.SetTile(cellPosition, null);
            OnInnocentCellDug(cellPosition);
        }
    }

    private bool TryFindConvertibleInnocentCell(Vector3Int origin, out Vector3Int targetCell)
    {
        List<Vector3Int> candidates = new List<Vector3Int>();

        for (int radius = 1; radius <= maxConversionSearchRadius; radius++)
        {
            candidates.Clear();

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    Vector3Int offset = new Vector3Int(x, y, 0);
                    if (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) != radius)
                    {
                        continue;
                    }

                    Vector3Int candidate = origin + offset;
                    if (IsConvertibleInnocentCell(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            if (candidates.Count > 0)
            {
                targetCell = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                return true;
            }
        }

        targetCell = Vector3Int.zero;
        return false;
    }

    private bool IsConvertibleInnocentCell(Vector3Int cellPosition)
    {
        if (cellsByGridPosition.ContainsKey(cellPosition))
        {
            return false;
        }

        if (innocentTumorTilemap == null)
        {
            return !convertOnlyInnocentTumorTiles;
        }

        return innocentTumorTilemap.HasTile(cellPosition);
    }

    private bool HasSignalPathThroughTumor(Vector3Int start, Vector3Int target, int maxDistance)
    {
        if (innocentTumorTilemap == null)
        {
            return true;
        }

        Queue<Vector3Int> open = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        open.Enqueue(start);
        visited.Add(start);

        while (open.Count > 0)
        {
            Vector3Int current = open.Dequeue();
            if (current == target)
            {
                return true;
            }

            if (ManhattanDistance(start, current) >= maxDistance)
            {
                continue;
            }

            foreach (Vector3Int dir in cardinalDirections)
            {
                Vector3Int next = current + dir;
                if (visited.Contains(next))
                {
                    continue;
                }

                bool canPass = innocentTumorTilemap.HasTile(next) || cellsByGridPosition.ContainsKey(next) || next == target;
                if (!canPass)
                {
                    continue;
                }

                visited.Add(next);
                open.Enqueue(next);
            }
        }

        return false;
    }

    private void DiscoverExistingTumorCells()
    {
        TumorCell[] foundCells = FindObjectsByType<TumorCell>(FindObjectsSortMode.None);
        foreach (TumorCell cell in foundCells)
        {
            RegisterCell(cell);
        }
    }

    private void FindReferencesIfMissing()
    {
        InitializeReferencesOnly();

        if (generatedCellsParent == null)
        {
            GameObject parent = new GameObject("Generated Tumor Cells");
            generatedCellsParent = parent.transform;
        }
    }

    private void InitializeReferencesOnly()
    {
        if (innocentTumorTilemap == null)
        {
            innocentTumorTilemap = FindGroundTilemap();
        }

        if (tumorCellPrefab == null)
        {
            tumorCellPrefab = FindFirstObjectByType<TumorCell>();
        }
    }

    private Tilemap FindGroundTilemap()
    {
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);

        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap.name.Equals("Ground", System.StringComparison.OrdinalIgnoreCase))
            {
                return tilemap;
            }
        }

        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                return tilemap;
            }
        }

        return tilemaps.Length > 0 ? tilemaps[0] : null;
    }

    private Transform FindPlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private void RecalculateCounters()
    {
        SenescentCount = 0;
        DormantCount = 0;
        AwakeCount = 0;

        foreach (TumorCell cell in cells)
        {
            if (cell == null || cell.IsDestroyed)
            {
                continue;
            }

            switch (cell.CurrentState)
            {
                case TumorCellState.Senescent:
                    SenescentCount++;
                    break;
                case TumorCellState.Dormant:
                    DormantCount++;
                    break;
                case TumorCellState.Awake:
                    AwakeCount++;
                    break;
            }
        }
    }

    private void CheckVictoryCondition()
    {
        if (endingTriggered || initialDangerousCellCount <= 0)
        {
            return;
        }

        if (DangerousCellCount <= 0)
        {
            endingTriggered = true;
            Debug.Log("[Tumor] Victoria: LINFO ha eliminado todas las células enemigas.");
            StartCoroutine(EndLevelRoutine(true));
        }
    }

    private void CheckDefeatCondition()
    {
        RecalculateCounters();
        if (!IsShuttingDown)
        {
            UpdateRuntimeUI();
        }

        if (endingTriggered || AwakeCount < maxAwakeCellsBeforeDefeat)
        {
            return;
        }

        TriggerDefeat("[Tumor] Derrota: demasiadas células despiertas. El tumor se ha reactivado.");
    }

    private void TriggerDefeat(string message)
    {
        if (endingTriggered)
        {
            return;
        }

        endingTriggered = true;
        Debug.Log(message);
        LinfoSoundPlayer.Play("Game_over_tumor_reactivation", 1f);
        StartCoroutine(EndLevelRoutine(false));
    }

    private IEnumerator EndLevelRoutine(bool victory)
    {
        yield return new WaitForSeconds(endStateDelay);

        if (IsShuttingDown)
        {
            yield break;
        }

        if (showEndScreenInsteadOfAutoReload)
        {
            ShowEndScreen(victory);
            yield break;
        }

        if (victory)
        {
            ContinueAfterVictory();
        }
        else
        {
            RetryCurrentLevel();
        }
    }

    private int ManhattanDistance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private void DrawSignalLine(Vector3 from, Vector3 to)
    {
        DrawSignalLine(from, to, signalLineColor);
    }

    private void DrawSignalLine(Vector3 from, Vector3 to, Color color)
    {
        if (!Application.isPlaying || IsShuttingDown)
        {
            return;
        }

        Debug.DrawLine(from, to, color, signalLineDuration);
        StartCoroutine(SignalLineRoutine(from, to, color));
    }

    private IEnumerator SignalLineRoutine(Vector3 from, Vector3 to, Color color)
    {
        if (IsShuttingDown)
        {
            yield break;
        }

        if (signalLineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                signalLineMaterial = new Material(shader);
            }
        }

        GameObject go = new GameObject("Signal Line");
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = signalLineWidth;
        line.endWidth = signalLineWidth;
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0f);
        line.sortingOrder = 50;

        if (signalLineMaterial != null)
        {
            line.material = signalLineMaterial;
        }

        yield return new WaitForSeconds(signalLineDuration);
        if (go != null && !IsShuttingDown)
        {
            Destroy(go);
        }
    }


    private void ShowEndScreen(bool victory)
    {
        if (IsShuttingDown || !Application.isPlaying)
        {
            return;
        }

        endScreenVictory = victory;
        isEndScreenVisible = true;

        EnsureEventSystemExists();

        if (endScreenRoot != null)
        {
            Destroy(endScreenRoot);
            endScreenRoot = null;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Runtime End UI Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        endScreenRoot = new GameObject(victory ? "Victory End Screen" : "Defeat End Screen");
        endScreenRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = endScreenRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlay = endScreenRoot.AddComponent<Image>();
        overlay.color = endScreenBackgroundColor;
        overlay.raycastTarget = true;

        GameObject panelGO = new GameObject("End Screen Panel");
        panelGO.transform.SetParent(endScreenRoot.transform, false);
        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(720f, 460f);

        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0.04f, 0.05f, 0.09f, 0.96f);

        TextMeshProUGUI title = CreateTMPText(
            "End Screen Title",
            panelGO.transform,
            victory ? "VICTORIA" : "DERROTA",
            54,
            TextAlignmentOptions.Center
        );
        title.color = victory ? victoryTitleColor : defeatTitleColor;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -42f);
        titleRect.sizeDelta = new Vector2(0f, 70f);

        string bodyText = victory
            ? $"LINFO ha eliminado todas las células enemigas.\nEl tumor queda controlado.\n\nEnemigos restantes: {DangerousCellCount}\nCélulas despiertas: {AwakeCount}/{maxAwakeCellsBeforeDefeat}"
            : $"El tumor se ha reactivado o LINFO ha perdido todas sus vidas.\n\nVidas: {currentPlayerLives}/{maxPlayerLives}\nCélulas despiertas: {AwakeCount}/{maxAwakeCellsBeforeDefeat}";

        TextMeshProUGUI body = CreateTMPText("End Screen Body", panelGO.transform, bodyText, 24, TextAlignmentOptions.Center);
        body.color = new Color(0.92f, 0.94f, 1f, 1f);
        RectTransform bodyRect = body.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -130f);
        bodyRect.sizeDelta = new Vector2(-80f, 150f);

        Button retryButton = CreateUIButton(
            "Retry Button",
            panelGO.transform,
            "RETRY NIVEL",
            retryButtonColor,
            new Vector2(0f, 140f),
            new Vector2(300f, 62f)
        );
        retryButton.onClick.AddListener(RetryCurrentLevel);

        if (victory && HasNextBuildScene())
        {
            Button continueButton = CreateUIButton(
                "Continue Button",
                panelGO.transform,
                "SIGUIENTE NIVEL",
                buttonColor,
                new Vector2(0f, 62f),
                new Vector2(300f, 62f)
            );
            continueButton.onClick.AddListener(ContinueAfterVictory);
        }
        else if (victory)
        {
            TextMeshProUGUI done = CreateTMPText("Campaign Finished Label", panelGO.transform, "No hay más niveles en Build Settings.", 18, TextAlignmentOptions.Center);
            done.color = new Color(0.75f, 0.85f, 1f, 1f);
            RectTransform doneRect = done.rectTransform;
            doneRect.anchorMin = new Vector2(0.5f, 0f);
            doneRect.anchorMax = new Vector2(0.5f, 0f);
            doneRect.pivot = new Vector2(0.5f, 0.5f);
            doneRect.anchoredPosition = new Vector2(0f, 62f);
            doneRect.sizeDelta = new Vector2(520f, 36f);
        }

        Time.timeScale = 0f;
    }

    private Button CreateUIButton(string name, Transform parent, string label, Color backgroundColor, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent, false);

        RectTransform rect = buttonGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonGO.AddComponent<Image>();
        image.color = backgroundColor;
        image.raycastTarget = true;

        Button button = buttonGO.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = new Color(
            Mathf.Min(backgroundColor.r + 0.18f, 1f),
            Mathf.Min(backgroundColor.g + 0.18f, 1f),
            Mathf.Min(backgroundColor.b + 0.18f, 1f),
            backgroundColor.a
        );
        colors.pressedColor = new Color(
            backgroundColor.r * 0.75f,
            backgroundColor.g * 0.75f,
            backgroundColor.b * 0.75f,
            backgroundColor.a
        );
        button.colors = colors;

        TextMeshProUGUI text = CreateTMPText($"{name} Label", buttonGO.transform, label, 24, TextAlignmentOptions.Center);
        text.color = Color.white;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private void EnsureEventSystemExists()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<InputSystemUIInputModule>();
    }

    public void RetryCurrentLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ContinueAfterVictory()
    {
        Time.timeScale = 1f;

        int activeBuildIndex = SceneManager.GetActiveScene().buildIndex;
        if (activeBuildIndex >= 0 && activeBuildIndex + 1 < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(activeBuildIndex + 1);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private bool HasNextBuildScene()
    {
        int activeBuildIndex = SceneManager.GetActiveScene().buildIndex;
        return activeBuildIndex >= 0 && activeBuildIndex + 1 < SceneManager.sceneCountInBuildSettings;
    }

    private void CreateRuntimeUIIfNeeded()
    {
        if (IsShuttingDown || !Application.isPlaying)
        {
            return;
        }

        if (awakeCellsSlider != null)
        {
            return;
        }

        Canvas existingCanvas = FindFirstObjectByType<Canvas>();
        Canvas canvas = existingCanvas;

        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Runtime Tumor UI Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        GameObject root = new GameObject("Awake Cells Runtime UI");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = awakeBarAnchoredPosition;
        rootRect.sizeDelta = new Vector2(awakeBarSize.x, 82f);

        objectiveLabel = CreateTMPText("Objective Label", root.transform, "Elimina todas las células enemigas", 18, TextAlignmentOptions.Center);
        RectTransform objectiveRect = objectiveLabel.rectTransform;
        objectiveRect.anchorMin = new Vector2(0f, 1f);
        objectiveRect.anchorMax = new Vector2(1f, 1f);
        objectiveRect.pivot = new Vector2(0.5f, 1f);
        objectiveRect.anchoredPosition = new Vector2(0f, 0f);
        objectiveRect.sizeDelta = new Vector2(0f, 22f);

        GameObject sliderGO = new GameObject("Awake Cells Bar");
        sliderGO.transform.SetParent(root.transform, false);
        awakeCellsSlider = sliderGO.AddComponent<Slider>();
        awakeCellsSlider.minValue = 0f;
        awakeCellsSlider.maxValue = 1f;
        awakeCellsSlider.interactable = false;

        RectTransform sliderRect = sliderGO.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 1f);
        sliderRect.anchorMax = new Vector2(1f, 1f);
        sliderRect.pivot = new Vector2(0.5f, 1f);
        sliderRect.anchoredPosition = new Vector2(0f, -28f);
        sliderRect.sizeDelta = awakeBarSize;

        Image bg = sliderGO.AddComponent<Image>();
        bg.color = awakeBarBackgroundColor;
        awakeCellsSlider.targetGraphic = bg;

        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(3f, 3f);
        fillAreaRect.offsetMax = new Vector2(-3f, -3f);

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        awakeCellsSliderFill = fillGO.AddComponent<Image>();
        awakeCellsSliderFill.color = awakeBarSafeFillColor;

        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        awakeCellsSlider.fillRect = fillRect;

        awakeCellsLabel = CreateTMPText("Awake Cells Label", sliderGO.transform, "", 16, TextAlignmentOptions.Center);
        RectTransform labelRect = awakeCellsLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        livesLabel = CreateTMPText("Lives Label", root.transform, "", 18, TextAlignmentOptions.Center);
        RectTransform livesRect = livesLabel.rectTransform;
        livesRect.anchorMin = new Vector2(0f, 1f);
        livesRect.anchorMax = new Vector2(1f, 1f);
        livesRect.pivot = new Vector2(0.5f, 1f);
        livesRect.anchoredPosition = new Vector2(0f, -60f);
        livesRect.sizeDelta = new Vector2(0f, 22f);
    }

    private TextMeshProUGUI CreateTMPText(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private void UpdateRuntimeUI()
    {
        if (IsShuttingDown || !Application.isPlaying || isEndScreenVisible)
        {
            return;
        }

        if (autoCreateAwakeProgressUI && awakeCellsSlider == null && Application.isPlaying)
        {
            CreateRuntimeUIIfNeeded();
        }

        float normalizedAwake = maxAwakeCellsBeforeDefeat > 0
            ? Mathf.Clamp01((float)AwakeCount / maxAwakeCellsBeforeDefeat)
            : 0f;

        if (awakeCellsSlider != null)
        {
            awakeCellsSlider.value = normalizedAwake;
        }

        if (awakeCellsSliderFill != null)
        {
            awakeCellsSliderFill.color = normalizedAwake >= 0.65f ? awakeBarFillColor : awakeBarSafeFillColor;
        }

        if (awakeCellsLabel != null)
        {
            awakeCellsLabel.text = $"Células despiertas: {AwakeCount} / {maxAwakeCellsBeforeDefeat}";
        }

        if (livesLabel != null)
        {
            livesLabel.text = $"Vidas de LINFO: {currentPlayerLives} / {maxPlayerLives}  |  Enemigos: {DangerousCellCount}";
        }

        if (objectiveLabel != null)
        {
            objectiveLabel.text = "Elimina todas las células enemigas antes de la reactivación tumoral";
        }
    }

    private void OnGUI()
    {
        if (!showOnGuiDebugHud)
        {
            return;
        }

        const int width = 360;
        const int height = 135;
        GUILayout.BeginArea(new Rect(12, 12, width, height), GUI.skin.box);
        GUILayout.Label("LINFO - Control tumoral");
        GUILayout.Label($"Vidas: {currentPlayerLives}/{maxPlayerLives}");
        GUILayout.Label($"Enemigos restantes: {DangerousCellCount}");
        GUILayout.Label($"Senescentes: {SenescentCount} | Dormentes: {DormantCount}");
        GUILayout.Label($"Despiertas: {AwakeCount} / {maxAwakeCellsBeforeDefeat}");
        GUILayout.Label("WASD/Flechas: mover | Espacio: aguijón/arpón");
        GUILayout.EndArea();
    }
}
