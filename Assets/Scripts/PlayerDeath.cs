using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerDeath : MonoBehaviour
{
    private bool isDead = false;

    public void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        Debug.Log("El jugador ha muerto.");

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}