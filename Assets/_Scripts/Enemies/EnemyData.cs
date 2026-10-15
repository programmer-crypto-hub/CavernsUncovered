using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyData",
    menuName = "Game Data/Enemy Data",
    order = 1)]
public class EnemyData : ScriptableObject
{
    public enum EnemyTypeByWeapons
    {
        Melee = 1,
        Ranged = 2,
        Boss = 3
    }

    public enum EnemyTypeByHealth
    {
        Basic = 1,
        Medium = 2,
        Boss = 3
    }

    public string enemyName = "New Enemy";

    public EnemyTypeByWeapons enemyType;
    public EnemyTypeByHealth healthTier;

    [Min(1f)]
    public float maxHealth = 200f;
    [Min(0f)]
    public float moveSpeed = 400f;
    [Min(0f)]
    public float damage = 25f;
    [Min(0f)]
    public float attackRange = 50f;
    [Min(0f)]
    public float detectionRange = 1000f;
    [Min(0f)]
    public float experienceReward = 500f;

    public GameObject prefab;
}