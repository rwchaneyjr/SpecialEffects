#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates a playable maze puzzle in the currently open scene.
/// Menu: Window → Maze Puzzle → Setup Scene
/// </summary>
public static class MazeGameSceneSetup
{
    const string ManagerName = "MazeGameManager";

    [MenuItem("Window/Maze Puzzle/Setup Scene")]
    [MenuItem("Tools/Maze Puzzle/Setup Scene")]
    public static void SetupScene()
    {
        EnsureCamera();
        var manager = EnsureManager();
        EnsureGround();

        // Generate maze into the hierarchy so it can be saved.
        manager.GenerateAndPlace();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = manager.gameObject;
        Debug.Log("Maze Puzzle Setup complete.");
    }

    static void EnsureCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
        }

        cam.transform.position = new Vector3(0f, 24f, -24f);
        cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.15f, 0.08f, 0.12f);
    }

    static MazeGameManager EnsureManager()
    {
        var existing = Object.FindObjectOfType<MazeGameManager>();
        if (existing != null)
            return existing;

        var go = new GameObject(ManagerName);
        return go.AddComponent<MazeGameManager>();
    }

    static void EnsureGround()
    {
        // Optional: a simple ground plane to receive lighting/collisions.
        var ground = GameObject.Find("Ground");
        if (ground != null)
            return;

        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Ground";
        plane.transform.position = new Vector3(0f, 0f, 0f);
        plane.transform.localScale = Vector3.one * 10f;
    }
}
#endif

