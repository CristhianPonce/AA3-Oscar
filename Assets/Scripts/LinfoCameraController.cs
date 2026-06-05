using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(900)]
[RequireComponent(typeof(Camera))]
public class LinfoCameraController : MonoBehaviour
{
    public static bool IsMapViewActive { get; private set; }

    [Header("Seguimiento normal")]
    [SerializeField] private Transform target;
    [SerializeField] private float closeOrthographicSize = 4.6f;
    [SerializeField] private float followSmoothTime = 0.12f;
    [SerializeField] private Vector2 followOffset = Vector2.zero;

    [Header("Vista mapa")]
    [SerializeField] private Key toggleMapViewKey = Key.Tab;
    [SerializeField] private Key alternativeToggleMapViewKey = Key.M;
    [SerializeField] private float mapPadding = 1.5f;
    [SerializeField] private float minMapOrthographicSize = 7f;
    [SerializeField] private float zoomSmoothTime = 0.15f;

    [Header("Comportamiento")]
    [Tooltip("Si está activo, LINFO no puede moverse ni disparar mientras se ve el mapa completo.")]
    [SerializeField] private bool blockPlayerWhileMapViewIsActive = true;

    private Camera controlledCamera;
    private Vector3 followVelocity;
    private float zoomVelocity;
    private bool mapViewActive;
    private Bounds mapBounds;
    private bool hasMapBounds;
    private float desiredCloseOrthographicSize;
    private float desiredMapOrthographicSize;

    public bool BlocksPlayerInput => blockPlayerWhileMapViewIsActive && mapViewActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterAutoSetup()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        IsMapViewActive = false;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        if (mainCamera.GetComponent<LinfoCameraController>() == null)
        {
            mainCamera.gameObject.AddComponent<LinfoCameraController>();
        }
    }

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        controlledCamera.orthographic = true;
        desiredCloseOrthographicSize = closeOrthographicSize;
        FindTargetIfMissing();
        RecalculateMapBounds();
        ApplyInitialCloseCameraPosition();
    }

    private void OnDestroy()
    {
        if (mapViewActive)
        {
            IsMapViewActive = false;
        }
    }

    private void LateUpdate()
    {
        FindTargetIfMissing();
        HandleMapViewToggleInput();
        UpdateCameraMode();
    }

    private void HandleMapViewToggleInput()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        bool pressedPrimary = Keyboard.current[toggleMapViewKey].wasPressedThisFrame;
        bool pressedAlternative = Keyboard.current[alternativeToggleMapViewKey].wasPressedThisFrame;

        if (!pressedPrimary && !pressedAlternative)
        {
            return;
        }

        SetMapViewActive(!mapViewActive);
    }

    private void SetMapViewActive(bool active)
    {
        mapViewActive = active;
        IsMapViewActive = blockPlayerWhileMapViewIsActive && mapViewActive;

        if (mapViewActive)
        {
            RecalculateMapBounds();
        }
    }

    private void UpdateCameraMode()
    {
        if (controlledCamera == null)
        {
            return;
        }

        if (mapViewActive)
        {
            MoveCameraTo(GetMapViewPosition());
            SmoothZoomTo(GetMapOrthographicSize());
            return;
        }

        MoveCameraTo(GetFollowPosition());
        SmoothZoomTo(desiredCloseOrthographicSize);
    }

    private void MoveCameraTo(Vector3 targetPosition)
    {
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref followVelocity, followSmoothTime);
    }

    private void SmoothZoomTo(float targetSize)
    {
        controlledCamera.orthographicSize = Mathf.SmoothDamp(controlledCamera.orthographicSize, targetSize, ref zoomVelocity, zoomSmoothTime);
    }

    private Vector3 GetFollowPosition()
    {
        if (target == null)
        {
            return new Vector3(transform.position.x, transform.position.y, -10f);
        }

        return new Vector3(target.position.x + followOffset.x, target.position.y + followOffset.y, -10f);
    }

    private Vector3 GetMapViewPosition()
    {
        if (hasMapBounds)
        {
            return new Vector3(mapBounds.center.x, mapBounds.center.y, -10f);
        }

        return new Vector3(transform.position.x, transform.position.y, -10f);
    }

    private float GetMapOrthographicSize()
    {
        if (!hasMapBounds || controlledCamera == null)
        {
            return Mathf.Max(minMapOrthographicSize, desiredCloseOrthographicSize * 2f);
        }

        float aspect = Mathf.Max(0.01f, controlledCamera.aspect);
        float heightBasedSize = mapBounds.extents.y + mapPadding;
        float widthBasedSize = (mapBounds.extents.x / aspect) + mapPadding;
        desiredMapOrthographicSize = Mathf.Max(minMapOrthographicSize, heightBasedSize, widthBasedSize);
        return desiredMapOrthographicSize;
    }

    private void ApplyInitialCloseCameraPosition()
    {
        if (controlledCamera == null)
        {
            return;
        }

        controlledCamera.orthographicSize = desiredCloseOrthographicSize;
        transform.position = GetFollowPosition();
    }

    private void FindTargetIfMissing()
    {
        if (target != null)
        {
            return;
        }

        DigDugController playerController = FindFirstObjectByType<DigDugController>();
        if (playerController != null)
        {
            target = playerController.transform;
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            target = playerObject.transform;
        }
    }

    private void RecalculateMapBounds()
    {
        TilemapRenderer[] tilemapRenderers = FindObjectsByType<TilemapRenderer>(FindObjectsSortMode.None);
        hasMapBounds = false;
        mapBounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (TilemapRenderer tilemapRenderer in tilemapRenderers)
        {
            if (tilemapRenderer == null || !tilemapRenderer.enabled)
            {
                continue;
            }

            Bounds rendererBounds = tilemapRenderer.bounds;
            if (rendererBounds.size == Vector3.zero)
            {
                continue;
            }

            if (!hasMapBounds)
            {
                mapBounds = rendererBounds;
                hasMapBounds = true;
            }
            else
            {
                mapBounds.Encapsulate(rendererBounds);
            }
        }

        if (hasMapBounds)
        {
            return;
        }

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasMapBounds)
            {
                mapBounds = renderer.bounds;
                hasMapBounds = true;
            }
            else
            {
                mapBounds.Encapsulate(renderer.bounds);
            }
        }
    }
}
