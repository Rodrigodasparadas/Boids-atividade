using UnityEngine;

/// <summary>
/// Esfera que persegue o boid mais próximo. Não precisa de collider.
/// A fuga dos boids é calculada em Boid.ComputeForce (FleeForce).
/// </summary>
public class Predator : MonoBehaviour
{
    public FlockManager flock;
    [Tooltip("Mantenha menor que maxSpeed do bando.")]
    public float speed = 4f;

    void Start()
    {
        if (flock != null) flock.predator = transform;
    }

    void Update()
    {
        if (flock == null || flock.Boids.Count == 0) return;

        Vector3 p = transform.position;
        float best = float.MaxValue;
        Vector3 target = p;

        for (int i = 0; i < flock.Boids.Count; i++)
        {
            Vector3 bp = flock.Boids[i].Position;
            float d2 = (bp - p).sqrMagnitude;
            if (d2 < best) { best = d2; target = bp; }
        }

        Vector3 dir = target - p;
        if (dir.sqrMagnitude > 0.0001f)
            transform.position += dir.normalized * (speed * Time.deltaTime);
    }
}
