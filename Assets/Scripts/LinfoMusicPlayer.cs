using UnityEngine;
using UnityEngine.SceneManagement;

public class LinfoMusicPlayer : MonoBehaviour
{
    private static LinfoMusicPlayer instance;
    private static bool isQuitting;

    [SerializeField] private string musicClipName = "preview-specimen";
    [SerializeField] private float musicVolume = 0.20f;

    private AudioSource musicSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreateMusicPlayer()
    {
        isQuitting = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstance();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
        if (instance != null)
        {
            instance.PlayMusicIfNeeded();
        }
    }

    private static LinfoMusicPlayer EnsureInstance()
    {
        if (isQuitting)
        {
            return null;
        }

        if (instance != null)
        {
            return instance;
        }

        LinfoMusicPlayer existing = FindFirstObjectByType<LinfoMusicPlayer>();
        if (existing != null)
        {
            instance = existing;
            DontDestroyOnLoad(existing.gameObject);
            return instance;
        }

        GameObject go = new GameObject("LINFO Music Player");
        instance = go.AddComponent<LinfoMusicPlayer>();
        DontDestroyOnLoad(go);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SetupAudioSource();
        PlayMusicIfNeeded();
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void SetupAudioSource()
    {
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }
        }

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;
    }

    private void PlayMusicIfNeeded()
    {
        SetupAudioSource();

        if (musicSource.clip == null)
        {
            musicSource.clip = Resources.Load<AudioClip>($"Sounds/{musicClipName}");
        }

        if (musicSource.clip == null)
        {
            Debug.LogWarning($"[Audio] No se ha encontrado la música Resources/Sounds/{musicClipName}.");
            return;
        }

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }
}
