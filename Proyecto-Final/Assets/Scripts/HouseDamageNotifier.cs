using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HouseDamageNotifier : MonoBehaviour
{
    [Header("Referencias")]
    public LifeController houseLife;
    public DangerIndicator dangerIndicator;
    public Transform player;

    [Header("Ajustes")]
    [Tooltip("Distancia m�nima a la casa para ignorar la notificaci�n")]
    public float ignoreRadius = 5f;

    void Start()
    {
        if (houseLife != null)
            houseLife.onDamaged.AddListener(OnHouseDamaged);
    }

    void OnDestroy()
    {
        if (houseLife != null)
            houseLife.onDamaged.RemoveListener(OnHouseDamaged);
    }

    private void OnHouseDamaged(float damage, LifeController.DamageType damageType)
    {
        if (player == null || dangerIndicator == null)
            return;

        float dist = Vector3.Distance(player.position, houseLife.transform.position);
        if (dist <= ignoreRadius)
            return;

        // Only when the warning first appears, not on every hit while it is showing.
        if (!IsInvoking(nameof(DeactivateIndicator))) SoundManager.Instance?.Play("UI_Warning");
        dangerIndicator.Activate();

        CancelInvoke(nameof(DeactivateIndicator));
        Invoke(nameof(DeactivateIndicator), 2f);
    }

    private void DeactivateIndicator()
    {
        dangerIndicator.Deactivate();
    }

    private void OnDrawGizmosSelected()
    {
        if (houseLife == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(houseLife.transform.position, ignoreRadius);
    }
}
