using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// カットシーン用FBXの中身を診断してファイルに書き出すツール。
// メニュー: Tools > カットシーン診断
// ・FBXの階層(ノードとコンポーネント)
// ・クリップに入っている全カーブ(どのノードのどのプロパティを何キーで動かすか、値の範囲)
// を cutscene_clip_dump.txt (プロジェクト直下) に出力する。
public static class CutsceneClipDebug
{
	private static readonly string[] Paths =
	{
		"Assets/FBX/Animation/Cutscene/cameraGameStart_v1.fbx",
		"Assets/FBX/Animation/Cutscene/cameraBossStart_v1.fbx",
		"Assets/FBX/Animation/Cutscene/cameraBossEnd_v1.fbx",
		"Assets/FBX/Animation/Cutscene/animationGameStart_v1.fbx",
	};

	[MenuItem("Tools/カットシーン診断")]
	public static void Dump()
	{
		var sb = new StringBuilder();

		foreach (string path in Paths)
		{
			sb.AppendLine($"===== {path} =====");

			GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (go == null)
			{
				sb.AppendLine("  !! アセットが見つからない");
				continue;
			}

			sb.AppendLine("[階層]");
			DumpHierarchy(go.transform, "", sb);

			foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
			{
				var clip = o as AnimationClip;
				if (clip == null || clip.name.StartsWith("__preview__")) continue;

				sb.AppendLine($"[クリップ] {clip.name} length={clip.length:F2}s rate={clip.frameRate} legacy={clip.legacy} humanMotion={clip.humanMotion}");

				var bindings = AnimationUtility.GetCurveBindings(clip);
				if (bindings.Length == 0) sb.AppendLine("  !! カーブが1本も無い");

				foreach (var b in bindings)
				{
					AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, b);
					float min = float.MaxValue, max = float.MinValue;
					foreach (var k in curve.keys)
					{
						min = Mathf.Min(min, k.value);
						max = Mathf.Max(max, k.value);
					}
					bool moves = (max - min) > 0.0005f;
					sb.AppendLine($"  path='{b.path}' type={b.type.Name} prop={b.propertyName} keys={curve.length} 範囲=[{min:F3}..{max:F3}]{(moves ? " ★変化あり" : " (一定)")}");
				}
			}
			sb.AppendLine();
		}

		string outPath = "cutscene_clip_dump.txt";
		File.WriteAllText(outPath, sb.ToString());
		Debug.Log($"[カットシーン診断] 書き出し完了: {Path.GetFullPath(outPath)}");
	}

	private static void DumpHierarchy(Transform t, string indent, StringBuilder sb)
	{
		string comps = string.Join(",", System.Array.ConvertAll(
			t.GetComponents<Component>(), c => c == null ? "(Missing)" : c.GetType().Name));
		sb.AppendLine($"  {indent}{t.name} [{comps}]");
		foreach (Transform c in t) DumpHierarchy(c, indent + "  ", sb);
	}
}
