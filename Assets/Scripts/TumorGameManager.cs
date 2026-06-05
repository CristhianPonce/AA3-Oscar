using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(-900)]
public class TumorGameManager : MonoBehaviour
{
    public static TumorGameManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private Tilemap innocentTumorTilemap;
    [SerializeField] private TumorCell tumorCellPrefab;
    [SerializeField] private Transform generatedCellsParent;

    [Header("Condiciones de partida")]
    [SerializeField] private int maxAwakeCellsBeforeDefeat = 20;
    [SerializeField] private float endStateDelay = 1.2f;

    [Header("Propagación")]
    [SerializeField] private int maxConversionSearchRadius = 2;
    [SerializeField] private int dormantsAwakenedPerSignal = 1;
    [SerializeField] private bool signalsRequireTumorPath = false;
    [SerializeField] private bool convertOnlyInnocentTumorTiles = true;

    [Header("Auto setup para prototipo")]
    [SerializeField] private bool autoSeedDormantCellsWhenMissing = true;
    [SerializeField] private int autoDormantCellsToSeed = 6;
    [SerializeField] private int minDistanceFromPlayerForAutoSeeds = 4;

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
    private int initialSenescentCount;

    public int SenescentCount { get; private set; }
    public int DormantCount { get; private set; }
    public int AwakeCount { get; private set; }
    public int MaxAwakeCellsBeforeDefeat => maxAwakeCellsBeforeDefeat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateAfterSceneLoad()
    {
        CreateRuntimeManager();
    }

    public static TumorGameManager CreateRuntimeManager()
    {
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
    }

    private void Start()
    {
        InitializeSceneIfNeeded();
    }

    private void OnDestroy()
    {
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
        AutoSeedDormantCellsIfNeeded();
        RecalculateCounters();
        initialSenescentCount = SenescentCount;

        Debug.Log($"[Tumor] Nivel listo. Senescentes: {SenescentCount}, Dormentes: {DormantCount}, Despiertas: {AwakeCount}. Límite de derrota: {maxAwakeCellsBeforeDefeat}.");
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
    }

    public void NotifyCellStateChanged(TumorCell cell)
    {
        if (cell == null)
        {
            return;
        }

        RegisterCell(cell);
        RecalculateCounters();
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

        if (destroyedState == TumorCellState.Senescent && killedByLinfo)
        {
            Debug.Log("[Tumor] LINFO ha eliminado una célula senescente.");
        }

        CheckVictoryCondition();
    }

    public void OnInnocentCellDug(Vector3Int cellPosition)
    {
        // La propia Tilemap ya ha eliminado la célula inocua. Este hook queda preparado
        // para puntuación, partículas o registro científico si se amplía el juego.
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
        if (innocentTumorTilemap != null && innocentTumorTilemap.HasTile(targetCell))
        {
            innocentTumorTilemap.SetTile(targetCell, null);
        }

        TumorCell newCell = CreateCell(targetCell, TumorCellState.Awake, "Awake Cell");
        if (newCell != null)
        {
            Debug.Log($"[Tumor] Célula despierta multiplicada en {targetCell}.");
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

    private void AutoSeedDormantCellsIfNeeded()
    {
        if (!autoSeedDormantCellsWhenMissing || DormantCount > 0 || tumorCellPrefab == null || innocentTumorTilemap == null)
        {
            return;
        }

        List<Vector3Int> candidates = new List<Vector3Int>();
        BoundsInt bounds = innocentTumorTilemap.cellBounds;
        Transform player = FindPlayerTransform();
        Vector3Int playerCell = player != null ? WorldToCell(player.position) : Vector3Int.zero;

        foreach (Vector3Int cellPosition in bounds.allPositionsWithin)
        {
            if (!innocentTumorTilemap.HasTile(cellPosition) || cellsByGridPosition.ContainsKey(cellPosition))
            {
                continue;
            }

            if (player != null && ManhattanDistance(playerCell, cellPosition) < minDistanceFromPlayerForAutoSeeds)
            {
                continue;
            }

            candidates.Add(cellPosition);
        }

        int amount = Mathf.Min(autoDormantCellsToSeed, candidates.Count);
        for (int i = 0; i < amount; i++)
        {
            int index = UnityEngine.Random.Range(0, candidates.Count);
            Vector3Int chosen = candidates[index];
            candidates.RemoveAt(index);

            if (innocentTumorTilemap.HasTile(chosen))
            {
                innocentTumorTilemap.SetTile(chosen, null);
            }

            CreateCell(chosen, TumorCellState.Dormant, "Dormant Cell");
        }

        RecalculateCounters();
        Debug.Log($"[Tumor] Auto setup: creadas {amount} células dormentes de prototipo.");
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
        if (endingTriggered || initialSenescentCount <= 0)
        {
            return;
        }

        if (SenescentCount <= 0)
        {
            endingTriggered = true;
            Debug.Log("[Tumor] Victoria: LINFO ha eliminado todas las células senescentes antes de la reactivación tumoral.");
            StartCoroutine(EndLevelRoutine(true));
        }
    }

    private void CheckDefeatCondition()
    {
        RecalculateCounters();

        if (endingTriggered || AwakeCount < maxAwakeCellsBeforeDefeat)
        {
            return;
        }

        endingTriggered = true;
        Debug.Log("[Tumor] Derrota: demasiadas células despiertas. El tumor se ha reactivado.");
        StartCoroutine(EndLevelRoutine(false));
    }

    private IEnumerator EndLevelRoutine(bool victory)
    {
        yield return new WaitForSeconds(endStateDelay);

        int activeBuildIndex = SceneManager.GetActiveScene().buildIndex;
        if (victory && activeBuildIndex >= 0 && activeBuildIndex + 1 < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(activeBuildIndex + 1);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
        if (!Application.isPlaying)
        {
            return;
        }

        Debug.DrawLine(from, to, color, signalLineDuration);
        StartCoroutine(SignalLineRoutine(from, to, color));
    }

    private IEnumerator SignalLineRoutine(Vector3 from, Vector3 to, Color color)
    {
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
        Destroy(go);
    }

    private void OnGUI()
    {
        if (!showOnGuiDebugHud)
        {
            return;
        }

        const int width = 350;
        const int height = 112;
        GUILayout.BeginArea(new Rect(12, 12, width, height), GUI.skin.box);
        GUILayout.Label("LINFO - Control tumoral");
        GUILayout.Label($"Senescentes restantes: {SenescentCount}");
        GUILayout.Label($"Dormentes: {DormantCount}");
        GUILayout.Label($"Despiertas: {AwakeCount} / {maxAwakeCellsBeforeDefeat}");
        GUILayout.Label("WASD/Flechas: mover | Espacio: aguijón/arpón");
        GUILayout.EndArea();
    }
}
