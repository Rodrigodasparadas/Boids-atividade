using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Instancia o bando, guarda todos os parâmetros ajustáveis no Inspector e
/// executa o passo de simulação dos boids em FixedUpdate.
/// </summary>
public class FlockManager : MonoBehaviour
{
    [Header("Spawn")]
    public Boid boidPrefab;
    [Min(1)] public int boidCount = 200;
    public float spawnRadius = 8f;
    [Tooltip("A layer precisa existir em Project Settings > Tags and Layers.")]
    public string boidLayerName = "Boid";

    [Header("Camadas")]
    [Tooltip("Se ficar vazio, usa apenas a layer Boid.")]
    public LayerMask boidMask;
    public LayerMask obstacleMask;

    [Header("Pesos das regras")]
    public float separationWeight = 1.5f;
    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.0f;

    [Header("Percepção")]
    public float perceptionRadius = 2.5f;
    public float separationRadius = 1.2f;
    [Range(8, 256)] public int maxNeighbors = 64;

    [Header("Movimento")]
    public float maxSpeed = 5f;
    public float minSpeed = 2f;
    public float maxForce = 10f;
    public float turnSpeed = 10f;

    [Header("Limites do mundo (centro = posição deste objeto)")]
    public float worldRadius = 20f;
    public float boundsWeight = 3f;

    [Header("Obstáculos")]
    public float avoidDistance = 4f;
    public float avoidRadius = 0.4f;
    public float obstacleWeight = 4f;

    [Header("Predador (opcional)")]
    public Transform predator;
    public float fleeRadius = 6f;
    public float fleeWeight = 8f;
    public float fleeSpeedBoost = 1.3f;

    [Header("Grid espacial (Parte C)")]
    [Tooltip("Pode ser alternado durante o Play para comparar no Profiler.")]
    public bool useSpatialGrid = false;
    public float cellSize = 2f;

    [Header("Debug")]
    public bool showHud = true;

    // ---- acessos usados pelos boids ----
    public Collider[] NeighborBuffer { get; private set; }
    public Dictionary<Vector3Int, List<Boid>> Grid { get; } = new Dictionary<Vector3Int, List<Boid>>();
    public int CellRange { get; private set; } = 1;
    public Vector3 WorldCenter => transform.position;
    public IReadOnlyList<Boid> Boids => _boids;

    readonly List<Boid> _boids = new List<Boid>();
    int _boidLayer;
    float _invCell = 0.5f;

    static readonly ProfilerMarker TickMarker = new ProfilerMarker("Flock.Tick");
    static readonly ProfilerMarker GridMarker = new ProfilerMarker("Flock.RebuildGrid");

    // HUD
    float _acc;
    int _frames;
    string _hud = "";

    void Awake()
    {
        _boidLayer = LayerMask.NameToLayer(boidLayerName);
        if (_boidLayer < 0)
        {
            Debug.LogError($"[FlockManager] A layer '{boidLayerName}' não existe. " +
                           "Crie em Project Settings > Tags and Layers.", this);
            enabled = false;
            return;
        }

        if (boidMask.value == 0) boidMask = 1 << _boidLayer;
        if (boidPrefab == null)
        {
            Debug.LogError("[FlockManager] Atribua o prefab do boid.", this);
            enabled = false;
            return;
        }

        // Boids não colidem entre si (a separação é feita pela regra, não pela física)
        Physics.IgnoreLayerCollision(_boidLayer, _boidLayer, true);

        NeighborBuffer = new Collider[maxNeighbors]; // criado uma vez e reutilizado

        Spawn();
    }

    void Spawn()
    {
        for (int i = 0; i < boidCount; i++)
        {
            Vector3 pos = WorldCenter + Random.insideUnitSphere * spawnRadius;
            Boid b = Instantiate(boidPrefab, pos, Random.rotation, transform);
            b.gameObject.layer = _boidLayer;
            b.Init(this, i == 0); // só o primeiro desenha o raio de percepção
            _boids.Add(b);
        }
    }

    void FixedUpdate()
    {
        using (TickMarker.Auto())
        {
            float dt = Time.fixedDeltaTime;

            for (int i = 0; i < _boids.Count; i++) _boids[i].Sample();

            if (useSpatialGrid) RebuildGrid();

            for (int i = 0; i < _boids.Count; i++) _boids[i].Tick(dt);
        }
    }

    // ------------------------------------------------------------------
    // Grid espacial
    // ------------------------------------------------------------------
    public Vector3Int CellOf(Vector3 p)
    {
        return new Vector3Int(
            Mathf.FloorToInt(p.x * _invCell),
            Mathf.FloorToInt(p.y * _invCell),
            Mathf.FloorToInt(p.z * _invCell));
    }

    void RebuildGrid()
    {
        using (GridMarker.Auto())
        {
            cellSize = Mathf.Max(0.1f, cellSize);
            _invCell = 1f / cellSize;
            CellRange = Mathf.Max(1, Mathf.CeilToInt(perceptionRadius / cellSize));

            // Esvazia as listas existentes em vez de recriá-las (sem lixo para o GC)
            foreach (var list in Grid.Values) list.Clear();

            for (int i = 0; i < _boids.Count; i++)
            {
                Vector3Int key = CellOf(_boids[i].Position);
                if (!Grid.TryGetValue(key, out var cell))
                {
                    cell = new List<Boid>(8);
                    Grid.Add(key, cell);
                }
                cell.Add(_boids[i]);
            }
        }
    }

    // ------------------------------------------------------------------
    // HUD simples para screenshots do README (atualiza texto a cada 0,5 s)
    // ------------------------------------------------------------------
    void Update()
    {
        _acc += Time.unscaledDeltaTime;
        _frames++;
        if (_acc >= 0.5f)
        {
            float fps = _frames / _acc;
            _hud = $"Boids: {_boids.Count}  |  {(useSpatialGrid ? "Grid" : "OverlapSphere")}  |  {fps:F0} FPS ({1000f / fps:F1} ms)";
            _acc = 0f;
            _frames = 0;
        }
    }

    void OnGUI()
    {
        if (showHud) GUI.Label(new Rect(10, 10, 600, 25), _hud);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, worldRadius);
    }
}
