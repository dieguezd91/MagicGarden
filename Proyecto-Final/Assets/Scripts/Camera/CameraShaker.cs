using System.Collections;
using UnityEngine;

public class CameraShaker : MonoBehaviour
{
    public static CameraShaker Instance;

    [SerializeField] private CameraOffsetController cameraOffsetController;
    [SerializeField] private AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [Tooltip("When on, a new shake only replaces the running one if it is at least as strong as the running shake right now. Off = the last call always wins (previous behavior).")]
    [SerializeField] private bool strongestShakeWins = true;

    // Intensity applied this frame; 0 when no shake is running.
    private float currentIntensity;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Shake(float intensity, float duration)
    {
        if (strongestShakeWins && intensity < currentIntensity) return;
        StopAllCoroutines();
        StartCoroutine(ShakeRoutine(intensity, duration));
    }

    private IEnumerator ShakeRoutine(float intensity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (Time.timeScale == 0f)
            {
                yield return null;
                continue;
            }

            float normalizedTime = elapsed / duration;
            currentIntensity = intensity * shakeCurve.Evaluate(normalizedTime);

            Vector3 offset = (Vector3)Random.insideUnitCircle * currentIntensity;
            cameraOffsetController?.SetOffset(offset);

            elapsed += Time.deltaTime;
            yield return null;
        }

        currentIntensity = 0f;
        cameraOffsetController?.ResetOffset();
    }

    private void OnDisable()
    {
        currentIntensity = 0f;
    }

}