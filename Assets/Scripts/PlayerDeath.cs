using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerDeath : MonoBehaviour
{
    private bool isDead = false;
    private bool isInvulnerable = false;
    private Vector3 respawnPosition;
    private Coroutine invulnerabilityRoutine;
    private Rigidbody2D rb;
    private SpriteRenderer[] spriteRenderers;
    private Collider2D[] colliders;
    private DigDugController controller;

    public bool IsInvulnerable => isInvulnerable;

    private void Awake()
    {
        respawnPosition = transform.position;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
        controller = GetComponent<DigDugController>();
    }

    public void Die()
    {
        if (isDead || isInvulnerable)
        {
            return;
        }

        isDead = true;

        if (controller == null)
        {
            controller = GetComponent<DigDugController>();
        }

        if (controller != null)
        {
            controller.PrepareForDamageRespawn();
        }

        TumorGameManager manager = TumorGameManager.Instance;
        if (manager != null)
        {
            manager.OnPlayerHit(this);
            return;
        }

        Debug.Log("El jugador ha muerto.");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Respawn(float invulnerabilityTime)
    {
        if (invulnerabilityRoutine != null)
        {
            StopCoroutine(invulnerabilityRoutine);
            invulnerabilityRoutine = null;
        }

        invulnerabilityRoutine = StartCoroutine(RespawnRoutine(invulnerabilityTime));
    }

    private IEnumerator RespawnRoutine(float invulnerabilityTime)
    {
        isInvulnerable = true;

        if (controller == null)
        {
            controller = GetComponent<DigDugController>();
        }

        if (controller != null)
        {
            controller.SetExternalInputLock(true);
        }

        SetSpritesVisible(true);
        SetCollidersEnabled(false);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        yield return new WaitForFixedUpdate();

        transform.position = respawnPosition;

        if (rb != null)
        {
            rb.position = respawnPosition;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }

        Physics2D.SyncTransforms();

        if (controller != null)
        {
            controller.ResetMovementAfterRespawn();
            controller.SetExternalInputLock(false);
        }

        yield return new WaitForFixedUpdate();

        SetCollidersEnabled(true);
        Physics2D.SyncTransforms();

        isDead = false;

        yield return StartCoroutine(InvulnerabilityRoutine(invulnerabilityTime));

        invulnerabilityRoutine = null;
    }

    private IEnumerator InvulnerabilityRoutine(float duration)
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            visible = !visible;
            SetSpritesVisible(visible);
            yield return new WaitForSeconds(0.12f);
            elapsed += 0.12f;
        }

        SetSpritesVisible(true);
        isInvulnerable = false;
    }

    private void SetSpritesVisible(bool visible)
    {
        if (spriteRenderers == null)
        {
            return;
        }

        foreach (SpriteRenderer renderer in spriteRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (colliders == null)
        {
            return;
        }

        foreach (Collider2D col in colliders)
        {
            if (col != null)
            {
                col.enabled = enabled;
            }
        }
    }
}
