using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class DropEnemyMaterial : MonoBehaviour
{
    [SerializeField] private CraftingMaterialSO materialData;
    [SerializeField] private float attractionSpeed = 8f;
    [SerializeField] private bool isSpecialItem = false;
    public UnityEvent onSpecialItemCollected;
    private Transform player;
    private bool isCollected = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isCollected) return;
        if (collision.gameObject.layer != 7) return;

        player = collision.transform;
        isCollected = true;
        GetComponent<Collider2D>().enabled = false;
        StartCoroutine(MoveToPlayerAndCollect());
    }

    private IEnumerator MoveToPlayerAndCollect()
    {
        while (Vector2.Distance(transform.position, player.position) > 0.1f)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                player.position,
                attractionSpeed * Time.deltaTime
            );
            yield return null;
        }

        if (materialData != null && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddMaterial(materialData.materialType, 1);
        }
        var pickupHandler = player.GetComponentInChildren<FloatingTextController>();
        if (pickupHandler != null && materialData != null)
        {
            // ShowPickup plays the (throttled) pickup sound.
            pickupHandler.ShowPickup(materialData.materialName, 1, materialData.materialIcon);
        }
        else
        {
            SoundManager.Instance?.PlayOneShot("PickUp_Material");
        }


        if (isSpecialItem)
        {
            if (LevelManager.Instance != null)
            {
                UnityEngine.Object.FindObjectOfType<GameOverController>()?.ShowContinuePanel();
            }
            else
            {
                Debug.LogWarning("LevelManager no encontrado para activar la pantalla de CONTINUAR�.");
            }
        }


        Destroy(gameObject);
    }
}
