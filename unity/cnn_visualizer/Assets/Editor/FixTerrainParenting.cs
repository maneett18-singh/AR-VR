using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Editor utility: finds TerrainCollider objects whose GameObject is parented under a
// Rigidbody or ArticulationBody and reparents them to the scene root. Run from
// Tools -> Fix Terrain Parenting -> Fix Terrains In Open Scenes
public static class FixTerrainParenting
{
    [MenuItem("Tools/Fix Terrain Parenting/Fix Terrains In Open Scenes")]
    public static void FixTerrainsInOpenScenes()
    {
        int moved = 0;
        var terrains = Object.FindObjectsOfType<TerrainCollider>(true);
        foreach (var tc in terrains)
        {
            if (tc == null) continue;
            var t = tc.transform;
            Transform ancestor = t.parent;
            bool shouldUnparent = false;
            while (ancestor != null)
            {
                if (ancestor.GetComponent<Rigidbody>() != null || ancestor.GetComponent<ArticulationBody>() != null)
                {
                    shouldUnparent = true;
                    break;
                }
                ancestor = ancestor.parent;
            }

            if (shouldUnparent)
            {
                Undo.SetTransformParent(t, null, "Fix Terrain Parenting - Unparent Terrain");
                EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                Debug.Log($"FixTerrainParenting: Unparented Terrain '{t.name}' from Rigidbody/Articulation ancestor.");
                moved++;
            }
        }

        if (moved == 0)
            Debug.Log("FixTerrainParenting: No problematic Terrains found in open scenes.");
        else
            Debug.Log($"FixTerrainParenting: Reparented {moved} Terrain GameObject(s) to scene root. Don't forget to save your scenes.");
    }
}
