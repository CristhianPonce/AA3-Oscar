using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class AmmoReader : MonoBehaviour
{
    private TextMeshProUGUI ammoText;
    private DigDugController player;

    private void Awake()
    {
        ammoText = GetComponent<TextMeshProUGUI>();
    }

    private void Update()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<DigDugController>();
        }

        TumorGameManager manager = TumorGameManager.Instance;

        if (player == null)
        {
            ammoText.text = "LINFO no encontrado";
            return;
        }

        if (manager == null)
        {
            ammoText.text = $"Aguijones: {player.currentHarpoons}";
            return;
        }

        ammoText.text =
            $"Aguijones: {player.currentHarpoons}\n" +
            $"Vidas: {manager.PlayerLives}/{manager.MaxPlayerLives}\n" +
            $"Enemigos: {manager.DangerousCellCount}\n" +
            $"Senescentes: {manager.SenescentCount}\n" +
            $"Dormentes: {manager.DormantCount}\n" +
            $"Despiertas: {manager.AwakeCount}/{manager.MaxAwakeCellsBeforeDefeat}";
    }
}
