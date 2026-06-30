using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MeshBaker : MonoBehaviour
{
    public SkinnedMeshRenderer targetSkinnedMesh;

    [ContextMenu("ここで拳のメッシュを生成して保存")]
    public void BakeAndSaveNow()
    {
        if (targetSkinnedMesh == null)
        {
            Debug.LogWarning("Target Skinned Mesh が設定されていません！");
            return;
        }

        // メッシュを撮影する
        Mesh bakedMesh = new Mesh();
        targetSkinnedMesh.BakeMesh(bakedMesh);

        // 撮影したメッシュをAssetsフォルダに保存
#if UNITY_EDITOR
        AssetDatabase.CreateAsset(bakedMesh, "Assets/Baked_Fist_Mesh.asset");
        AssetDatabase.SaveAssets();
#endif
        GameObject bakedObj = new GameObject("Baked_Fist_Arm");
        bakedObj.AddComponent<MeshFilter>().mesh = bakedMesh;
        bakedObj.AddComponent<MeshRenderer>().material = targetSkinnedMesh.material;

        Debug.Log("メッシュの保存完了！");
    }
}