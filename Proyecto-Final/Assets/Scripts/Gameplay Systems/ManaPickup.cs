using UnityEngine;
using System.Collections;

public class ManaPickup : MonoBehaviour
{
    [SerializeField] private float manaAmountMin = 5f;
    [SerializeField] private float manaAmountMax = 15f;
    [SerializeField] private float attractionSpeed = 8f;
    [SerializeField] private Sprite manaSprite;

    private Transform player;
    private float manaAmount;
    private bool isCollected = false;

    private void Start()
    {
        manaAmount = Random.Range(manaAmountMin, manaAmountMax);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isCollected) return;
        if (!collision.CompareTag("Player")) return;

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

        var manaSystem = player.GetComponent<ManaSystem>();
        if (manaSystem != null)
        {
            manaSystem.AddMana(manaAmount);
        }

        var pickupHandler = player.GetComponentInChildren<FloatingTextController>();
        if (pickupHandler != null)
        {
            // ShowPickup plays the (throttled) pickup sound.
            pickupHandler.ShowPickup("Mana", Mathf.RoundToInt(manaAmount), manaSprite);
        }
        else
        {
            SoundManager.Instance?.PlayOneShot("PickUp_Mana");
        }

        Destroy(gameObject);
    }
}
