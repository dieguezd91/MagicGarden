using UnityEngine;
using TMPro;

public class FloatingDamageText : MonoBehaviour
{
    public Vector3 offset = new Vector3(0, 1f, 0);
    public float floatSpeed = 1f;
    public float fadeInDuration = 0.2f;
    public float duration = 1.5f;
    public float fadeOutDuration = 0.5f;
    public Color startColor = new Color(1f, 0.5f, 0f, 1f);
    public Color endColor = Color.red;
    public float maxDamageForColor = 50f;

    public float minScale = 0.5f;
    public float maxScale = 1f;

    [Header("KILL POP")]
    [Tooltip("The number pops when the hit kills its target. Untick to disable.")]
    public bool popOnKill = true;
    [Tooltip("Scale multiplier at the instant of the kill, before it settles.")]
    public float killPopScale = 1.8f;
    [Tooltip("Scale multiplier the number keeps after the pop (1 = same size as a normal hit).")]
    public float killSettleScale = 1.3f;
    [Tooltip("Seconds the pop takes to settle (real time). 0 skips the pop and uses the settle scale directly.")]
    public float killPopDuration = 0.25f;

    private TextMeshProUGUI textMesh;
    private Transform target;
    private float totalDamage = 0f;
    private float lastHitTime;
    private float spawnTime;
    private bool isFadingOut = false;
    private LifeController targetLife;
    private bool killed;
    private float killTime;

    private Vector3 initialWorldPosition;

    void Awake()
    {
        textMesh = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void Initialize(Transform target)
    {
        this.target = target;
        targetLife = target != null ? target.GetComponent<LifeController>() : null;
        transform.SetParent(target, worldPositionStays: true);
        transform.localPosition = offset;

        initialWorldPosition = transform.position;

        spawnTime = Time.time;
        lastHitTime = Time.time;
        totalDamage = 0f;

        textMesh.color = new Color(startColor.r, startColor.g, startColor.b, 0f);
        transform.localScale = Vector3.one * minScale;
    }

    void Update()
    {
        float now = Time.time;
        float sinceSpawn = now - spawnTime;
        float sinceLastHit = now - lastHitTime;

        float tDamage = Mathf.Clamp01(totalDamage / maxDamageForColor);

        Color dynamicColor = Color.Lerp(startColor, endColor, tDamage);

        float currentScale = Mathf.Lerp(minScale, maxScale, tDamage) * KillScaleMultiplier();
        transform.localScale = Vector3.one * currentScale;

        float totalFloatDistance = sinceSpawn * floatSpeed;

        if (!isFadingOut && target != null)
        {
            transform.position = target.position + offset + Vector3.up * totalFloatDistance;
        }
        else
        {
            transform.position += Vector3.up * floatSpeed * Time.deltaTime;
        }

        if (sinceSpawn <= fadeInDuration)
        {
            float aIn = sinceSpawn / fadeInDuration;
            textMesh.color = new Color(dynamicColor.r, dynamicColor.g, dynamicColor.b, aIn);
        }
        else if (!isFadingOut && sinceLastHit < duration)
        {
            textMesh.color = new Color(dynamicColor.r, dynamicColor.g, dynamicColor.b, 1f);
        }

        if (!isFadingOut && sinceLastHit >= duration)
        {
            isFadingOut = true;
            transform.SetParent(null, worldPositionStays: true);
        }

        if (isFadingOut)
        {
            float fadeElapsed = sinceLastHit - duration;
            float tOut = Mathf.Clamp01(fadeElapsed / fadeOutDuration);
            float aOut = Mathf.Lerp(1f, 0f, tOut);

            textMesh.color = new Color(dynamicColor.r, dynamicColor.g, dynamicColor.b, aOut);

            if (fadeElapsed >= fadeOutDuration)
                Destroy(gameObject);
        }
    }

    // 1 for a normal hit. After a kill: starts at killPopScale and eases down to killSettleScale.
    private float KillScaleMultiplier()
    {
        if (!killed) return 1f;
        if (killPopDuration <= 0f) return killSettleScale;
        float t = Mathf.Clamp01((Time.unscaledTime - killTime) / killPopDuration);
        float eased = 1f - (1f - t) * (1f - t) * (1f - t);
        return Mathf.Lerp(killPopScale, killSettleScale, eased);
    }

    public void AddDamage(float dmg)
    {
        totalDamage += dmg;
        if (popOnKill && !killed && targetLife != null && !targetLife.IsAlive())
        {
            killed = true;
            killTime = Time.unscaledTime;
        }
        textMesh.text = totalDamage.ToString("F0");
        lastHitTime = Time.time;

        if (isFadingOut)
        {
            isFadingOut = false;
            if (target != null)
                transform.SetParent(target, worldPositionStays: true);
        }
    }
}