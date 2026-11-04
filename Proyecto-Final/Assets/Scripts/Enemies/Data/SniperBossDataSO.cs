
using UnityEngine;

[CreateAssetMenu(
    fileName = "New Sniper Boss Data",
    menuName = "Enemy Data/Sniper Boss"
)]
public class SniperBossDataSO : BossEnemyDataSO
{
    [Header("SNIPER ATTACK")]
    [SerializeField] private float sniperAttackRange = 8f;
    [SerializeField] private float aimDuration = 1.2f;
    [SerializeField] private float shotInterval = 3.5f;
    [SerializeField] private float recoveryDuration = 0.3f;

    [Header("PROJECTILE")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 14f;
    [SerializeField] private float projectileDamage = 25f;
    [SerializeField] private float projectileLifetime = 5f;

    public float SniperAttackRange => sniperAttackRange;
    public float AimDuration => aimDuration;
    public float ShotInterval => shotInterval;
    public float RecoveryDuration => recoveryDuration;

    public GameObject ProjectilePrefab => projectilePrefab;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileDamage => projectileDamage;
    public float ProjectileLifetime => projectileLifetime;
}
