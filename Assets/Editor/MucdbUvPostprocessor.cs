using UnityEditor;
using UnityEngine;

/// <summary>
/// The source mucdb FBX contains a flat mesh whose exported UV coordinates are all (0, 0).
/// Generate stable planar UVs during import so its material can display ca_muc.png.
/// </summary>
internal sealed class MucdbUvPostprocessor : AssetPostprocessor
{
    private const string ModelPath = "Assets/prefabs/fish/mucdb/mucdb.fbx";

    [InitializeOnLoadMethod]
    private static void ReimportBrokenUvOnce()
    {
        EditorApplication.delayCall += () =>
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null || HasUsableUv(model))
                return;

            Debug.Log("[mucdb] Source UVs are empty; reimporting with generated planar UVs.");
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
        };
    }

    private void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.Equals(ModelPath, System.StringComparison.OrdinalIgnoreCase))
            return;

        int correctedMeshes = 0;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (GeneratePlanarUv(filter.sharedMesh))
                correctedMeshes++;
        }

        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (GeneratePlanarUv(renderer.sharedMesh))
                correctedMeshes++;
        }

        Debug.Log($"[mucdb] Generated planar UVs for {correctedMeshes} mesh(es).");
    }

    private static bool GeneratePlanarUv(Mesh mesh)
    {
        if (mesh == null || mesh.vertexCount == 0)
            return false;

        Vector3[] vertices = mesh.vertices;
        Bounds bounds = mesh.bounds;

        // mucdb is a flat XY character. Using its local bounds keeps the mapping independent
        // from the FBX root scale, scene position and animation bones.
        float width = Mathf.Max(bounds.size.x, 0.0001f);
        float height = Mathf.Max(bounds.size.y, 0.0001f);
        Vector2[] uv = new Vector2[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            uv[i] = new Vector2(
                Mathf.Clamp01((vertices[i].x - bounds.min.x) / width),
                Mathf.Clamp01((vertices[i].y - bounds.min.y) / height));
        }

        mesh.uv = uv;
        return true;
    }

    private static bool HasUsableUv(GameObject model)
    {
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (HasUvArea(filter.sharedMesh))
                return true;
        }

        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (HasUvArea(renderer.sharedMesh))
                return true;
        }

        return false;
    }

    private static bool HasUvArea(Mesh mesh)
    {
        if (mesh == null || mesh.vertexCount == 0 || mesh.uv == null ||
            mesh.uv.Length == 0 || mesh.uv.Length != mesh.vertexCount)
            return false;

        Vector2[] uv = mesh.uv;
        Vector2 min = uv[0];
        Vector2 max = uv[0];
        for (int i = 1; i < uv.Length; i++)
        {
            min = Vector2.Min(min, uv[i]);
            max = Vector2.Max(max, uv[i]);
        }

        return max.x - min.x > 0.001f && max.y - min.y > 0.001f;
    }
}
