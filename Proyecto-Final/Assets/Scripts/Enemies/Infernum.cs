using UnityEngine;

public class Infernum : EnemyBase
{
    [Header("Ranged Data")]
    [SerializeField] private RangedEnemyDataSO rangedData;

    [Header("Elemental Type")]
    [SerializeField] private bool isIceEnemy;

    [Header("Combat References")]
    [SerializeField] private Transform firingPoint;

    private float shootingRange;
    private float fireRate;
    private float aimYOffset;
    private float nextTimeToFire = 0f;
    private Vector3 firingPointOffset;

    // FireRate (RangedEnemyDataSO) is the number of seconds between two shots, like the other cooldowns in the enemy data.
    // The first shot is allowed right away (nextTimeToFire starts at 0).
    public bool CanShootNow => Time.time >= nextTimeToFire;

    protected override void Awake()
    {
        base.Awake();
        if (firingPoint != null)
            firingPointOffset = firingPoint.localPosition;
    }

    protected override EnemyDataSO GetEnemyData() => rangedData;

    protected override void LoadEnemyData()
    {
        base.LoadEnemyData();

        if (rangedData != null)
        {
            shootingRange = rangedData.ShootingRange;
            fireRate = rangedData.FireRate;
            aimYOffset = rangedData.AimYOffset;
        }
    }

    

    public void PerformAttack()
    {
        if (Time.time < nextTimeToFire) return;

        if (currentTarget == null) return;

        Shoot();
        nextTimeToFire = Time.time + Mathf.Max(0f, fireRate);
    }

    // It only turned while walking, so when it stopped to shoot it could face away from its target.
    // The sprite turns with flipX, which does not move children, so the firing point is mirrored by hand.
    private void FaceTarget()
    {
        float dx = currentTarget.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.01f) return;
        float side = Mathf.Sign(dx);
        UpdateSpriteDirection(new Vector2(side, 0f));
        if (firingPoint != null)
            firingPoint.localPosition = new Vector3(Mathf.Abs(firingPointOffset.x) * side, firingPointOffset.y, firingPointOffset.z);
    }

    private void Shoot()
    {
        FaceTarget();
        Vector2 adjustedTargetPos = (Vector2)currentTarget.position + Vector2.down * aimYOffset;
        Vector2 direction = (adjustedTargetPos - (Vector2)transform.position).normalized;

       
        FireBullet projectile = isIceEnemy ? BulletPool.Instance.GetIceBall() : BulletPool.Instance.GetBullet();

        projectile.transform.position = firingPoint.position;
        projectile.transform.rotation = firingPoint.rotation;
        projectile.SetDirection(direction);

        PlayEnemySound(EnemySoundType.Attack, SoundSourceType.Localized, transform);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, shootingRange);
    }

    protected override void ProcessMovement()
    {
    }
}
