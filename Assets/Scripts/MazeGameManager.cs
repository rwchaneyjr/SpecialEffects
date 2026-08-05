using System.Collections;
using UnityEngine;

/// <summary>
/// Simple Unity Maze Puzzle:
/// - Generates a grid maze (DFS backtracker)
/// - Places a player (CharacterController) at the start
/// - Places a goal at the end
/// - On win: plays a short winning melody (procedural audio)
/// </summary>
public class MazeGameManager : MonoBehaviour
{
    [Header("Maze Size")]
    [Min(2)] public int width = 12;
    [Min(2)] public int height = 12;
    public int seed = 1337;

    [Header("Building Style")]
    public float cellSize = 2f;
    public float wallHeight = 1.5f;
    public float wallThickness = 0.08f;
    public float floorY = 0f;

    [Header("Gameplay")]
    public Transform playerTransform;
    public Transform goalTransform;
    public float playerSpeed = 6f;
    public float goalRadius = 0.5f;

    [Header("Audio (winning tune)")]
    public bool enableWinTune = true;
    public AudioSource audioSource;
    public float tuneVolume = 0.35f;

    [Header("Objects in hierarchy")]
    public Transform mazeRoot;

    // Internal maze representation
    struct Cell
    {
        public bool visited;
        public bool wallN;
        public bool wallE;
        public bool wallS;
        public bool wallW;
    }

    Cell[,] _cells;

    bool _won;
    Coroutine _tuneRoutine;
    CharacterController _cc;

    void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        if (_won)
            return;

        EnsurePlayerController();
        if (_cc != null && playerTransform != null)
        {
            Vector3 move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            move = Vector3.ClampMagnitude(move, 1f);
            Vector3 delta = move * playerSpeed * Time.deltaTime;
            _cc.Move(delta);
        }

        if (goalTransform != null && playerTransform != null)
        {
            float d = Vector3.Distance(new Vector3(playerTransform.position.x, floorY, playerTransform.position.z),
                                         new Vector3(goalTransform.position.x, floorY, goalTransform.position.z));
            if (d <= goalRadius)
                Win();
        }
    }

    void EnsurePlayerController()
    {
        if (_cc != null)
            return;
        if (playerTransform == null)
            return;
        _cc = playerTransform.GetComponent<CharacterController>();
    }

    public void GenerateAndPlace()
    {
        _won = false;
        if (mazeRoot == null)
        {
            var rootGo = GameObject.Find("MazeRoot");
            mazeRoot = rootGo != null ? rootGo.transform : null;
        }

        if (mazeRoot == null)
        {
            var go = new GameObject("MazeRoot");
            mazeRoot = go.transform;
        }

        // Clear previous walls.
        for (int i = mazeRoot.childCount - 1; i >= 0; i--)
        {
            var child = mazeRoot.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }

        BuildMazeData();
        BuildMazeGeometry();
        PlacePlayerAndGoal();
    }

    void BuildMazeData()
    {
        _cells = new Cell[width, height];

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            _cells[x, y].visited = false;
            // Start with all walls present.
            _cells[x, y].wallN = true;
            _cells[x, y].wallE = true;
            _cells[x, y].wallS = true;
            _cells[x, y].wallW = true;
        }

        var rng = new System.Random(seed);
        var stack = new System.Collections.Generic.Stack<Vector2Int>();
        Vector2Int start = new Vector2Int(0, 0);
        stack.Push(start);
        _cells[start.x, start.y].visited = true;

        while (stack.Count > 0)
        {
            var current = stack.Peek();

            var unvisited = new System.Collections.Generic.List<Vector2Int>(4);
            // N
            if (current.y + 1 < height && !_cells[current.x, current.y + 1].visited) unvisited.Add(new Vector2Int(0, 1));
            // E
            if (current.x + 1 < width && !_cells[current.x + 1, current.y].visited) unvisited.Add(new Vector2Int(1, 0));
            // S
            if (current.y - 1 >= 0 && !_cells[current.x, current.y - 1].visited) unvisited.Add(new Vector2Int(0, -1));
            // W
            if (current.x - 1 >= 0 && !_cells[current.x - 1, current.y].visited) unvisited.Add(new Vector2Int(-1, 0));

            if (unvisited.Count == 0)
            {
                stack.Pop();
                continue;
            }

            var pick = unvisited[rng.Next(unvisited.Count)];
            var next = new Vector2Int(current.x + pick.x, current.y + pick.y);

            // Remove the wall between current and next.
            if (pick.x == 0 && pick.y == 1)
            {
                // N
                _cells[current.x, current.y].wallN = false;
                _cells[next.x, next.y].wallS = false;
            }
            else if (pick.x == 1 && pick.y == 0)
            {
                // E
                _cells[current.x, current.y].wallE = false;
                _cells[next.x, next.y].wallW = false;
            }
            else if (pick.x == 0 && pick.y == -1)
            {
                // S
                _cells[current.x, current.y].wallS = false;
                _cells[next.x, next.y].wallN = false;
            }
            else if (pick.x == -1 && pick.y == 0)
            {
                // W
                _cells[current.x, current.y].wallW = false;
                _cells[next.x, next.y].wallE = false;
            }

            _cells[next.x, next.y].visited = true;
            stack.Push(next);
        }
    }

    void BuildMazeGeometry()
    {
        // Coordinate system:
        // Cell (x,y) center is:
        //   worldX = (x * cellSize)
        //   worldZ = (y * cellSize)
        // Then we center the maze around (0,0).
        float mazeW = (width - 1) * cellSize;
        float mazeH = (height - 1) * cellSize;

        Vector3 CenterOffset()
        {
            return new Vector3(-mazeW * 0.5f, 0f, -mazeH * 0.5f);
        }

        Vector3 offset = CenterOffset();

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            Vector3 cellCenter = new Vector3(x * cellSize, floorY, y * cellSize) + offset;

            // Build north wall for every cell except the bottom-most edge duplicates are okay;
            // we’ll create N and W only, plus borders S/E at the end to avoid duplicates.
            if (_cells[x, y].wallN)
                CreateWall(new Vector3(cellCenter.x, floorY + wallHeight * 0.5f, cellCenter.z + cellSize * 0.5f),
                            new Vector3(cellSize, wallHeight, wallThickness), "WallN");

            if (_cells[x, y].wallW)
                CreateWall(new Vector3(cellCenter.x - cellSize * 0.5f, floorY + wallHeight * 0.5f, cellCenter.z),
                            new Vector3(wallThickness, wallHeight, cellSize), "WallW");

            // Border walls
            if (y == 0 && _cells[x, y].wallS)
                CreateWall(new Vector3(cellCenter.x, floorY + wallHeight * 0.5f, cellCenter.z - cellSize * 0.5f),
                            new Vector3(cellSize, wallHeight, wallThickness), "WallS");

            if (x == 0 && _cells[x, y].wallW)
            {
                // already handled by wallW, kept for clarity
            }

            if (x == width - 1 && _cells[x, y].wallE)
                CreateWall(new Vector3(cellCenter.x + cellSize * 0.5f, floorY + wallHeight * 0.5f, cellCenter.z),
                            new Vector3(wallThickness, wallHeight, cellSize), "WallE");

            if (y == 0 && _cells[x, y].wallS)
            {
                // already handled above
            }
        }
    }

    void CreateWall(Vector3 pos, Vector3 size, string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(mazeRoot, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(
            size.x / 1f,
            size.y / 1f,
            size.z / 1f
        );

        // Slightly reduce scale jitter by setting exact localScale.
        go.transform.localScale = size;

        // Keep materials simple.
        var r = go.GetComponent<Renderer>();
        if (r != null)
            r.sharedMaterial = null; // use default
    }

    void PlacePlayerAndGoal()
    {
        float mazeW = (width - 1) * cellSize;
        float mazeH = (height - 1) * cellSize;
        Vector3 offset = new Vector3(-mazeW * 0.5f, 0f, -mazeH * 0.5f);

        Vector3 startPos = new Vector3(0 * cellSize, floorY + 0.9f, 0 * cellSize) + offset;
        Vector3 goalPos = new Vector3((width - 1) * cellSize, floorY + 0.5f, (height - 1) * cellSize) + offset;

        if (playerTransform == null)
        {
            var playerGo = GameObject.Find("MazePlayer");
            if (playerGo != null)
                playerTransform = playerGo.transform;
            else
                playerTransform = CreatePlayer(startPos).transform;
        }
        else
        {
            playerTransform.position = startPos;
        }

        if (goalTransform == null)
        {
            var goalGo = GameObject.Find("MazeGoal");
            if (goalGo != null)
                goalTransform = goalGo.transform;
            else
                goalTransform = CreateGoal(goalPos).transform;
        }
        else
        {
            goalTransform.position = goalPos;
        }
    }

    GameObject CreatePlayer(Vector3 startPos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "MazePlayer";
        go.transform.position = startPos;

        var cc = go.AddComponent<CharacterController>();
        cc.radius = 0.4f;
        cc.height = 1.0f;
        cc.stepOffset = 0.2f;

        // Disable capsule collider (CharacterController handles collisions).
        var col = go.GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        return go;
    }

    GameObject CreateGoal(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "MazeGoal";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.8f;
        var r = go.GetComponent<Renderer>();
        if (r != null)
            r.sharedMaterial = null;
        return go;
    }

    void Win()
    {
        if (_won)
            return;
        _won = true;

        // Stop player movement by disabling controller input (simple).
        var cc = playerTransform != null ? playerTransform.GetComponent<CharacterController>() : null;
        if (cc != null)
            cc.enabled = false;

        if (enableWinTune)
        {
            if (_tuneRoutine != null)
                StopCoroutine(_tuneRoutine);
            _tuneRoutine = StartCoroutine(PlayWinTune());
        }
    }

    IEnumerator PlayWinTune()
    {
        // A short, simple melody (frequencies in Hz).
        // C5-ish, E5-ish, G5-ish, C6-ish
        float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.5f };
        float noteDur = 0.14f;
        float gap = 0.02f;

        foreach (var f in freqs)
        {
            float duration = noteDur;
            var clip = CreateToneClip(f, duration, tuneVolume);
            audioSource.PlayOneShot(clip);
            yield return new WaitForSeconds(duration + gap);
        }
    }

    AudioClip CreateToneClip(float frequency, float duration, float volume)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        var clip = AudioClip.Create("MazeTone", samples, 1, sampleRate, false);

        float[] data = new float[samples];
        float omega = 2f * Mathf.PI * frequency;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            // Sine wave with a quick fade-in/out to avoid clicks.
            float envIn = Mathf.Clamp01(t / 0.01f);
            float envOut = Mathf.Clamp01((duration - t) / 0.03f);
            float env = envIn * envOut;
            data[i] = Mathf.Sin(omega * t) * env * volume;
        }

        clip.SetData(data, 0);
        return clip;
    }
}

