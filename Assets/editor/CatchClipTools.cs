using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// ホールド移動の横方向クリップを整備するツール。
// メニュー: Tools > ホールド横クリップ生成と接続
// ・animationMainCatchRunSide_v1.fbx からクリップを複製して CatchRunSide_v1.anim を作成
// ・ループ/Root Transform設定を既存の正常クリップ(CatchRun_v1等)と同じ値に調整
// ・PlayerAnimationController の Hold ブレンドツリーの横(±1,0)スロットへ自動接続
public static class CatchClipTools
{
	private const string SourceFbx = "Assets/FBX/Animation/PlayerAnimation/ClonedAnimaiton/animationMainCatchRunSide_v1.fbx";
	private const string OutClip = "Assets/FBX/Animation/PlayerAnimation/CatchRunSide_v1.anim";
	private const string OutClipMirror = "Assets/FBX/Animation/PlayerAnimation/CatchRunSideMirror_v1.anim";
	private const string Controller = "Assets/FBX/Animation/PlayerAnimationController.controller";

	[MenuItem("Tools/ホールド横クリップ生成と接続")]
	public static void CreateAndWireSideClip()
	{
		// ① FBXの中のクリップを取得
		AnimationClip src = null;
		foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(SourceFbx))
		{
			if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
			{
				src = c;
				break;
			}
		}
		if (src == null)
		{
			Debug.LogError($"[CatchClipTools] クリップが見つからない: {SourceFbx}");
			return;
		}

		// ② 右移動用と、ミラーを焼き込んだ左移動用の2つを作る
		//    (BlendTree子のMirrorフラグは効かないことがあるため、クリップ設定でミラーする)
		AnimationClip right = DuplicateWithSettings(src, "CatchRunSide_v1", mirror: false, OutClip);
		AnimationClip left = DuplicateWithSettings(src, "CatchRunSideMirror_v1", mirror: true, OutClipMirror);

		// ③ Hold ブレンドツリーの横スロットへ接続(+1=右, -1=左ミラー)
		var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
		if (ctrl == null)
		{
			Debug.LogError($"[CatchClipTools] コントローラが見つからない: {Controller}");
			return;
		}

		int wired = 0;
		foreach (var layer in ctrl.layers)
		{
			foreach (var cs in layer.stateMachine.states)
			{
				if (cs.state.name != "Hold") continue;

				var tree = cs.state.motion as BlendTree;
				if (tree == null) continue;

				ChildMotion[] children = tree.children;
				for (int i = 0; i < children.Length; i++)
				{
					if (children[i].position.x > 0.5f)
					{
						children[i].motion = right;
						children[i].mirror = false;
						wired++;
					}
					else if (children[i].position.x < -0.5f)
					{
						children[i].motion = left;
						children[i].mirror = false; // ミラーはクリップ側に焼き込み済み
						wired++;
					}
				}
				tree.children = children;
				EditorUtility.SetDirty(tree);
			}
		}

		EditorUtility.SetDirty(ctrl);
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();

		Debug.Log($"[CatchClipTools] 完了: 右={OutClip} / 左(ミラー)={OutClipMirror} を作成し、Holdツリーへ{wired}箇所接続しました");
	}

	// FBXクリップを複製し、既存の正常クリップと同じループ/Root Transform設定+ミラー指定で保存する
	private static AnimationClip DuplicateWithSettings(AnimationClip src, string name, bool mirror, string outPath)
	{
		AnimationClip copy = Object.Instantiate(src);
		copy.name = name;

		AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(copy);
		s.loopTime = true;
		s.loopBlendOrientation = false;
		s.loopBlendPositionY = true;
		s.loopBlendPositionXZ = true;
		s.keepOriginalOrientation = false;
		s.keepOriginalPositionY = true;
		s.keepOriginalPositionXZ = false;
		s.heightFromFeet = false;
		s.mirror = mirror;
		AnimationUtility.SetAnimationClipSettings(copy, s);

		AssetDatabase.CreateAsset(copy, outPath);
		return copy;
	}
}
