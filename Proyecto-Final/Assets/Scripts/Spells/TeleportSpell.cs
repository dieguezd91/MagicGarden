using UnityEngine;

public class TeleportSpell : Spell
{
    [Header("TELEPORT SETTINGS")]
    [SerializeField] private float teleportDistance = 5f;
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private LayerMask teleportableLayers;

    [Header("VFX")]
    [SerializeField] private GameObject teleportStartEffectPrefab;
    [SerializeField] private GameObject teleportEndEffectPrefab;
    [SerializeField] private float effectDuration = 0.5f;

    public override MagicGarden.Target DescribeUpgradeTarget(string id, string displayName)
    {
        var target = new MagicGarden.Target { Id = id, Name = displayName, Capability = GetType().Name };
        target.Bases[MagicGarden.Stat.Range] = teleportDistance;
        return target;
    }
    private Transform playerTransform;
    private bool hasExecuted = false;

    public override void Cast(Vector2 direction, Vector3 spawnPosition)
    {
        if (hasExecuted) return;
        hasExecuted = true;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Destroy(gameObject);
            return;
        }

        playerTransform = player.transform;

        if (!TryGetTeleportDestination(
            player,
            direction,
            playerTransform.position,
            out Vector3 targetPosition))
        {
            Destroy(gameObject);
            return;
        }

        Vector3 startPosition = playerTransform.position;

        ExecuteTeleport(startPosition, targetPosition);

        Destroy(gameObject, effectDuration);
    }

    private void ExecuteTeleport(Vector3 startPos, Vector3 endPos)
    {
        ShowTeleportEffect(startPos, teleportStartEffectPrefab);

        if (playerTransform != null)
        {
            playerTransform.position = endPos;
        }

        ShowTeleportEffect(endPos, teleportEndEffectPrefab);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play("PlayerTeleport", SoundSourceType.Global, playerTransform);
        }

        if (Time.timeScale > 0f && CameraShaker.Instance != null)
        {
            CameraShaker.Instance.Shake(0.2f, 0.15f);
        }
    }

    private void ShowTeleportEffect(Vector3 position, GameObject effectPrefab)
    {
        if (effectPrefab == null) return;

        GameObject effect = Instantiate(effectPrefab, position, Quaternion.identity);
        Destroy(effect, effectDuration);
    }

    public bool CanTeleportTo(Vector2 direction, Vector3 startPosition)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return false;

        return TryGetTeleportDestination(
            player,
            direction,
            startPosition,
            out _
        );
    }

    private bool TryGetTeleportDestination(
        GameObject player,
        Vector2 direction,
        Vector3 startPosition,
        out Vector3 targetPosition)
    {
        float distance = UpgradeRuntime.Value(
            "ability:teleport",
            MagicGarden.Stat.Range,
            teleportDistance,
            "TeleportSpell"
        );

        Vector2 teleportDirection = direction.normalized;

        targetPosition = startPosition +
            (Vector3)(teleportDirection * distance);

        RaycastHit2D hit = Physics2D.Raycast(
            startPosition,
            teleportDirection,
            distance,
            collisionMask
        );

        if (hit.collider != null)
        {
            targetPosition = hit.point - teleportDirection * 0.5f;
        }

        return IsValidDestination(player, targetPosition);
    }

    private bool IsValidDestination(GameObject player, Vector3 targetPosition)
    {
        if (Physics2D.OverlapPoint(targetPosition, teleportableLayers) == null)
            return false;

        Collider2D[] playerColliders =
            player.GetComponentsInChildren<Collider2D>();

        if (playerColliders.Length == 0)
            return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(collisionMask);
        filter.useTriggers = true;

        Collider2D[] overlaps = new Collider2D[32];

        Vector3 originalPosition = player.transform.position;
        bool checkedCollider = false;

        try
        {
            player.transform.position = targetPosition;
            Physics2D.SyncTransforms();

            foreach (Collider2D playerCollider in playerColliders)
            {
                if (playerCollider == null ||
                    !playerCollider.enabled ||
                    playerCollider.isTrigger)
                    continue;

                checkedCollider = true;

                int overlapCount = playerCollider.OverlapCollider(
                    filter,
                    overlaps
                );

                for (int i = 0; i < overlapCount; i++)
                {
                    Collider2D other = overlaps[i];

                    if (other == null)
                        continue;

                    if (other.transform == player.transform ||
                        other.transform.IsChildOf(player.transform))
                        continue;

                    return false;
                }
            }
        }
        finally
        {
            player.transform.position = originalPosition;
            Physics2D.SyncTransforms();
        }

        return checkedCollider;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (playerTransform != null)
        {
            Gizmos.DrawWireSphere(playerTransform.position, teleportDistance);
        }
    }
}