using UnityEngine;

public class PunchArm : MonoBehaviour
{
    // 分離後コライダー
    [SerializeField] private BoxCollider singleBoxCollider;
    [SerializeField] private GameObject boneRoot;

    // 腕のモデル
    [SerializeField] private Transform ArmMesh;
    [SerializeField] private int attackDamage = 1;

    private void OnTriggerEnter(Collider other)
    {
        var playerController = other.GetComponent<Controller>();
        if (playerController != null)
        {
            playerController.TakeDamage(attackDamage);
        }
    }

    // 腕が分離したときに呼ぶ関数gaa
    public void DetachArm()
    {
        GameObject droppedArm = new GameObject("DroppedArm_Physical");
        droppedArm.transform.position = ArmMesh.position;
        droppedArm.transform.rotation = ArmMesh.rotation;
        SkinnedMeshRenderer smr = ArmMesh.GetComponent<SkinnedMeshRenderer>();

        if (smr != null)
        {
            // 今のポーズでメッシュを固定
            Mesh bakedMesh = new Mesh();
            smr.BakeMesh(bakedMesh);
            GameObject bakedVisual = new GameObject("Arm_BakedMesh");
            bakedVisual.transform.SetParent(droppedArm.transform, false);
            MeshFilter mf = bakedVisual.AddComponent<MeshFilter>();
            mf.sharedMesh = bakedMesh;
            MeshRenderer mr = bakedVisual.AddComponent<MeshRenderer>();
            mr.sharedMaterials = smr.sharedMaterials;
            smr.enabled = false; // スキニング版は非表示

            Debug.Log(droppedArm.transform.position);
            Debug.Log(bakedVisual.transform.localPosition);
            Debug.Log(singleBoxCollider.transform.localPosition);
        }
        else
        {
            // MeshRenderer だけなら reparent で足りる
            ArmMesh.SetParent(droppedArm.transform, true);
        }

        // 元の腕ボーン群にあるコライダーを全てOFFにする（元の当たり判定を消す）
        Collider[] allColliders = boneRoot.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in allColliders)
        {
            col.enabled = false;
        }

        // 分離用の箱コライダーを新しいオブジェクトに移動してONにする
        if (singleBoxCollider != null)
        {
            singleBoxCollider.transform.parent = droppedArm.transform;
            singleBoxCollider.gameObject.SetActive(true);
            singleBoxCollider.enabled = true;
        }

        // 新しいオブジェクトにRigidbodyを追加
        Rigidbody rb = droppedArm.AddComponent<Rigidbody>();

        //singleBoxCollider.transform.SetParent(droppedArm.transform, false);
        //singleBoxCollider.transform.localPosition = Vector3.zero;
        //singleBoxCollider.transform.localRotation = Quaternion.identity;

        // ボス本体のコライダーと腕がぶつかって荒ぶるのを防ぐ
        Collider bossMainCollider = transform.root.GetComponent<Collider>();
        if (bossMainCollider != null && singleBoxCollider != null)
        {
            Physics.IgnoreCollision(singleBoxCollider, bossMainCollider);
        }

        // 元のボーン(this.gameObject)は transform.parent = null; で切り離さず、
        // そのままボスの元に残します（透明な状態でアニメーションし続けるためバグりません）

        // プレイヤーが拾うスクリプトなどを追加する場合は rb.gameObject に対して行います
        // rb.gameObject.AddComponent<PickUpScript>();
    }
}