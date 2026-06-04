using TMPro;
using UnityEngine;

public class AmmoReader : MonoBehaviour
{
    TextMeshProUGUI ammoText;
    void Start()
    {
        ammoText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        DigDugController player = Object.FindFirstObjectByType<DigDugController>();
        ammoText.text = player.currentHarpoons.ToString();
    }
}
