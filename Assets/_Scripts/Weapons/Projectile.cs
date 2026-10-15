using UnityEngine;

public class Projectile : MonoBehaviour
{
    private float speed = 20f;
    private float maxDistance = 20f;
    private float damage = 10f;
    private LayerMask hitLayers;

    public void Setup(float damage, float maxDistance, float speed, LayerMask hitLayers)
    {
        this.damage = damage;
        this.maxDistance = maxDistance;
        this.speed = speed;
        this.hitLayers = hitLayers;
    }

    private Vector3 _startPosition;

    private void Start()
    {
        _startPosition = transform.position;
    }

    private void Update()
    {
        transform.position += transform.forward * (speed * Time.deltaTime);

        float traveled = Vector3.Distance(_startPosition, transform.position);
        if (traveled >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((hitLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = other.GetComponentInParent<IDamageable>();

        else damageable.TakeDamage(damage);

        Destroy(gameObject);
    }
}