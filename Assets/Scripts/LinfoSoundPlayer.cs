using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LinfoSoundPlayer : MonoBehaviour
{
    private static LinfoSoundPlayer instance;
    private static bool isShuttingDown;

    private AudioSource oneShotSource;
    [SerializeField] private float masterVolume = 0.35f;
    private readonly Dictionary<string, AudioClip> cachedClips = new Dictionary<string, AudioClip>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStatics()
    {
        instance = null;
        isShuttingDown = false;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        instance = null;
    }

    private static LinfoSoundPlayer Instance
    {
        get
        {
            if (isShuttingDown || !Application.isPlaying)
            {
                return null;
            }

            if (instance != null)
            {
                return instance;
            }

            GameObject go = new GameObject("LINFO Sound Player");
            instance = go.AddComponent<LinfoSoundPlayer>();
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
        oneShotSource.spatialBlend = 0f;
    }

    private void OnApplicationQuit()
    {
        isShuttingDown = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static void Play(string clipName, float volume = 1f)
    {
        LinfoSoundPlayer player = Instance;
        if (player == null)
        {
            return;
        }

        AudioClip clip = player.GetClip(clipName);
        if (clip == null || player.oneShotSource == null)
        {
            return;
        }

        player.oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume * player.masterVolume));
    }

    public static AudioClip LoadClip(string clipName)
    {
        LinfoSoundPlayer player = Instance;
        return player != null ? player.GetClip(clipName) : null;
    }

    private AudioClip GetClip(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
        {
            return null;
        }

        if (cachedClips.TryGetValue(clipName, out AudioClip cached))
        {
            return cached;
        }

        AudioClip clip = Resources.Load<AudioClip>($"Sounds/{clipName}");
        if (clip == null)
        {
            Debug.LogWarning($"[Audio] No se ha encontrado Resources/Sounds/{clipName}.");
        }

        cachedClips[clipName] = clip;
        return clip;
    }
}
