
using UnityEngine;

[CreateAssetMenu(
    fileName = "New Turret Minion Data",
    menuName = "Enemy Data/Turret Minion"
)]
public class TurretMinionDataSO : EnemyDataSO
{
    [Header("TURRET ATTACK")]
    [SerializeField] private float attackRange = 4.5f;

    [Header("MACHINE GUN")]
    [SerializeField] private int shotsPerBurst = 4;
    [SerializeField] private float timeBetweenShots = 0.12f;
    [SerializeField] private float burstCooldown = 2.5f;

    [Header("PROJECTILE")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float projectileDamage = 5f;
    [SerializeField] private float projectileLifetime = 4f;

    public float AttackRange => attackRange;

    public int ShotsPerBurst => shotsPerBurst;
    public float TimeBetweenShots => timeBetweenShots;
    public float BurstCooldown => burstCooldown;

    public GameObject ProjectilePrefab => projectilePrefab;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileDamage => projectileDamage;
    public float ProjectileLifetime => projectileLifetime;
}
