using UnityEngine;

/// <summary>
/// Um agente do bando. Não tem Update/FixedUpdate próprio: o FlockManager chama
/// Sample() em todos os boids e depois Tick(), assim todos leem o mesmo "snapshot"
/// de posições/velocidades, independente da ordem de execução.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class Boid : MonoBehaviour
{
    [HideInInspector] public FlockManager manager;

    [Tooltip("Desenha o raio de percepção deste boid (Gizmos precisa estar ligado).")]
    public bool debugGizmo;

    /// <summary>Posição lida no início do passo de física.</summary>
    public Vector3 Position { get; private set; }
    /// <summary>Velocidade lida no início do passo de física.</summary>
    public Vector3 Velocity { get; private set; }

    Rigidbody _rb;
    RaycastHit _hit;

    // Acumuladores da percepção (campos para não alocar nada por frame)
    Vector3 _sepSum, _aliSum, _cohSum;
    int _neighborCount;
    float _perceptionSqr, _separationSqr;
    float _speedLimit;

    const float MinSeparationSqr = 0.0025f; // evita 1/d² explodir quando d -> 0
    const float LookRotationThreshold = 0.01f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;
        _rb.freezeRotation = true; // a rotação é controlada por código
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // não atravessa obstáculos estáticos
    }

    public void Init(FlockManager owner, bool showGizmo)
    {
        manager = owner;
        debugGizmo = showGizmo;
        _rb.linearVelocity = Random.onUnitSphere * owner.minSpeed;
        Sample();
    }

    public void Sample()
    {
        Position = _rb.position;
        Velocity = _rb.linearVelocity;
    }

    // ------------------------------------------------------------------
    // Passo de simulação
    // ------------------------------------------------------------------
    public void Tick(float dt)
    {
        FlockManager m = manager;

        // Rede de segurança: se algo virou NaN/Infinito, reaparece no bando
        if (!IsFinite(_rb.position) || !IsFinite(_rb.linearVelocity))
        {
            Respawn();
            return;
        }

        Vector3 force = ComputeForce(); // já limitada por maxForce e livre de NaN
        Vector3 v = Velocity + force * dt;

        if (!IsFinite(v)) v = Velocity;

        // Limite de velocidade (e velocidade mínima para o bando não "parar")
        float speed = v.magnitude;
        if (speed > _speedLimit)
        {
            v *= _speedLimit / speed;
        }
        else if (speed < m.minSpeed)
        {
            Vector3 dir = speed > 0.001f ? v / speed : transform.forward;
            v = dir * m.minSpeed;
        }

        _rb.linearVelocity = v;

        // LookRotation só com velocidade significativa
        if (v.sqrMagnitude > LookRotationThreshold * LookRotationThreshold)
        {
            Quaternion target = Quaternion.LookRotation(v);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, target, m.turnSpeed * dt));
        }
    }

    /// <summary>
    /// Soma ponderada de: separação + alinhamento + coesão + limites do mundo +
    /// obstáculos + fuga do predador. O resultado é limitado por maxForce.
    /// </summary>
    public Vector3 ComputeForce()
    {
        FlockManager m = manager;
        Vector3 pos = Position;
        Vector3 vel = Velocity;

        _speedLimit = m.maxSpeed;
        _sepSum = _aliSum = _cohSum = Vector3.zero;
        _neighborCount = 0;
        _perceptionSqr = m.perceptionRadius * m.perceptionRadius;
        _separationSqr = m.separationRadius * m.separationRadius;

        if (m.useSpatialGrid) GatherFromGrid(pos);
        else GatherFromPhysics(pos);

        Vector3 force = Vector3.zero;

        if (_neighborCount > 0)
        {
            float inv = 1f / _neighborCount;

            // Separação: soma de (direção de afastamento * 1/d²) -> mais perto, empurrão maior
            force += _sepSum * m.separationWeight;

            // Alinhamento: ajustar a velocidade para a média dos vizinhos
            Vector3 avgVel = _aliSum * inv;
            force += Vector3.ClampMagnitude(avgVel - vel, m.maxForce) * m.alignmentWeight;

            // Coesão: ir em direção ao centro de massa local
            Vector3 center = _cohSum * inv;
            force += Steer(center - pos, vel) * m.cohesionWeight;
        }

        force += BoundsForce(pos);
        force += ObstacleForce(pos, vel);
        force += FleeForce(pos, vel);

        if (!IsFinite(force)) return Vector3.zero;
        return Vector3.ClampMagnitude(force, m.maxForce);
    }

    // ------------------------------------------------------------------
    // Percepção
    // ------------------------------------------------------------------
    void GatherFromPhysics(Vector3 pos)
    {
        FlockManager m = manager;
        Collider[] buffer = m.NeighborBuffer; // mesmo array reutilizado por todos
        int n = Physics.OverlapSphereNonAlloc(pos, m.perceptionRadius, buffer, m.boidMask,
                                              QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (buffer[i].TryGetComponent(out Boid other)) Accumulate(other, pos);
        }
    }

    void GatherFromGrid(Vector3 pos)
    {
        FlockManager m = manager;
        Vector3Int c = m.CellOf(pos);
        int r = m.CellRange;

        for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
                for (int z = -r; z <= r; z++)
                {
                    if (!m.Grid.TryGetValue(new Vector3Int(c.x + x, c.y + y, c.z + z), out var cell)) continue;
                    for (int k = 0; k < cell.Count; k++) Accumulate(cell[k], pos);
                }
    }

    void Accumulate(Boid other, Vector3 pos)
    {
        if (other == this) return;

        Vector3 offset = pos - other.Position;
        float d2 = offset.sqrMagnitude;
        if (d2 > _perceptionSqr || d2 < 1e-8f) return;

        _neighborCount++;
        _aliSum += other.Velocity;
        _cohSum += other.Position;

        if (d2 < _separationSqr)
        {
            float d = Mathf.Sqrt(d2);
            _sepSum += (offset / d) * (1f / Mathf.Max(d2, MinSeparationSqr)); // 1/d²
        }
    }

    // ------------------------------------------------------------------
    // Forças extras
    // ------------------------------------------------------------------
    Vector3 BoundsForce(Vector3 pos)
    {
        FlockManager m = manager;
        Vector3 off = pos - m.WorldCenter;
        float dist = off.magnitude;
        if (dist <= m.worldRadius) return Vector3.zero; // dentro do raio: força zero

        return (-off / dist) * ((dist - m.worldRadius) * m.boundsWeight);
    }

    Vector3 ObstacleForce(Vector3 pos, Vector3 vel)
    {
        FlockManager m = manager;
        Vector3 fwd = vel.sqrMagnitude > 0.0001f ? vel.normalized : transform.forward;

        if (!Physics.SphereCast(pos, m.avoidRadius, fwd, out _hit, m.avoidDistance,
                                m.obstacleMask, QueryTriggerInteraction.Ignore))
            return Vector3.zero;

        Vector3 normal = _hit.normal;

        // Reflect devolve o boid; somar a componente tangente faz ele contornar em vez de quicar
        Vector3 reflect = Vector3.Reflect(fwd, normal);
        Vector3 slide = Vector3.ProjectOnPlane(fwd, normal);
        if (slide.sqrMagnitude < 0.01f)
        {
            slide = Vector3.Cross(normal, Vector3.up);
            if (slide.sqrMagnitude < 0.01f) slide = Vector3.Cross(normal, Vector3.right);
        }

        Vector3 dir = (reflect + slide.normalized).normalized;
        float proximity = 1f - Mathf.Clamp01(_hit.distance / m.avoidDistance);

        return Steer(dir, vel) * (m.obstacleWeight * Mathf.Lerp(0.3f, 1f, proximity));
    }

    Vector3 FleeForce(Vector3 pos, Vector3 vel)
    {
        FlockManager m = manager;
        if (m.predator == null) return Vector3.zero;

        Vector3 off = pos - m.predator.position;
        float d2 = off.sqrMagnitude;
        float r = m.fleeRadius;
        if (d2 >= r * r || d2 < 1e-6f) return Vector3.zero;

        float d = Mathf.Sqrt(d2);
        float urgency = 1f - d / r; // 0 na borda, 1 em cima do predador
        _speedLimit = m.maxSpeed * m.fleeSpeedBoost;

        return Steer(off / d, vel) * (m.fleeWeight * (0.5f + urgency));
    }

    /// <summary>Força de direção (Reynolds): velocidade desejada - velocidade atual.</summary>
    Vector3 Steer(Vector3 toward, Vector3 vel)
    {
        if (toward.sqrMagnitude < 1e-6f) return Vector3.zero;
        Vector3 desired = toward.normalized * manager.maxSpeed;
        return Vector3.ClampMagnitude(desired - vel, manager.maxForce);
    }

    // ------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------
    void Respawn()
    {
        FlockManager m = manager;
        _rb.position = m.WorldCenter + Random.insideUnitSphere * m.spawnRadius;
        _rb.linearVelocity = Random.onUnitSphere * m.minSpeed;
        Sample();
    }

    static bool IsFinite(Vector3 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }

    void OnDrawGizmos()
    {
        if (!debugGizmo || manager == null) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, manager.perceptionRadius);

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, manager.separationRadius);

        if (_rb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, _rb.linearVelocity);
        }
    }
}