using UnityEngine;

public static class FCVisualizerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureFcVisualizer()
    {
        var existing = Object.FindObjectOfType<FCNetworkVisualizer>();
        if (existing != null)
        {
            EnsureWsManagerWiring(existing);
            return;
        }

        var go = new GameObject("FCNetworkVisualizer_Auto");
        var visualizer = go.AddComponent<FCNetworkVisualizer>();

        // Make sure it builds its default network so something is visible immediately.
        visualizer.buildOnStart = true;

        EnsureWsManagerWiring(visualizer);
    }

    private static void EnsureWsManagerWiring(FCNetworkVisualizer visualizer)
    {
        if (visualizer == null)
            return;

        var ws = Object.FindObjectOfType<WSManager>();
        if (ws != null && ws.fcVisualizer == null)
        {
            ws.fcVisualizer = visualizer;
        }
    }
}
