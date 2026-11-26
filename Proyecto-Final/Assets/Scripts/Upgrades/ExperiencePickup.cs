using UnityEngine;

// Created by LifeController.Drop alongside materials and mana. Death never grants XP directly.
public sealed class ExperiencePickup : MonoBehaviour
{
    public const string DefaultPrefabResourcePath = "ExperienceItem";

    [Header("Collection")]
    [Min(0.1f)]
    [Tooltip("Speed at which the orb flies toward the player once attracted.")]
    [SerializeField] private float attractionSpeed = 8f;

    [Min(0.01f)]
    [Tooltip("Distance at which the orb is collected by the player.")]
    [SerializeField] private float collectDistance = 0.15f;

    [Tooltip("SoundManager key played when the orb is collected.")]
    [SerializeField] private string pickupSound = "PickUp";

    private int amount;
    private PlayerExperienceSystem collector;
    private bool collected;

    public static GameObject Spawn(Vector3 position, int amount, Sprite existingIcon)
        => Spawn(position, amount, existingIcon, null);

    public static GameObject Spawn(Vector3 position, int amount, Sprite existingIcon, GameObject prefabOverride)
    {
        var prefab = prefabOverride != null ? prefabOverride : Resources.Load<GameObject>(DefaultPrefabResourcePath);
        if (prefab != null)
        {
            var instance = Instantiate(prefab, position, Quaternion.identity);
            var pickup = instance.GetComponent<ExperiencePickup>() ?? instance.AddComponent<ExperiencePickup>();
            pickup.amount = amount;
            if (existingIcon != null && instance.TryGetComponent(out SpriteRenderer prefabRenderer) && prefabRenderer.sprite == null)
            {
                prefabRenderer.sprite = existingIcon;
            }
            return instance;
        }

        var item = new GameObject("Experience pickup");
        item.transform.position = position;
        item.transform.localScale = Vector3.one * 0.3f;
        var renderer = item.AddComponent<SpriteRenderer>();
        renderer.sprite = existingIcon;
        renderer.color = new Color(0.55f, 1f, 0.7f);
        renderer.sortingOrder = 20;
        var body = item.AddComponent<Rigidbody2D>();
        body.gravityScale = 0;
        body.drag = 5;
        var trigger = item.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 2f;
        item.AddComponent<ExperiencePickup>().amount = amount;
        return item;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || collector != null) return;
        collector = other.GetComponentInParent<PlayerExperienceSystem>();
    }

    private void Update()
    {
        if (collector == null || collected || UpgradeRuntime.GameplayBlocked) return;
        transform.position = Vector3.MoveTowards(transform.position, collector.transform.position, attractionSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, collector.transform.position) > collectDistance) return;
        collected = true;
        collector.AddExperience(amount);
        if (!string.IsNullOrEmpty(pickupSound))
        {
            SoundManager.Instance?.PlayOneShot(pickupSound);
        }
        Destroy(gameObject);
    }
}
