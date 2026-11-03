using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad]
public static class LightningSetup
{
 static LightningSetup(){EditorApplication.delayCall+=Setup;}
 static void Setup(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)return;
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="Stage_city")return;
  foreach(var t in Object.FindObjectsOfType<PolygonLightning>(true))if(t.gameObject.scene==scene)return;
  PolygonWallBuilder wall=null;
  foreach(var rain in Object.FindObjectsOfType<PolygonRainArea>(true))if(rain.gameObject.scene==scene){wall=new SerializedObject(rain).FindProperty("polygonWall").objectReferenceValue as PolygonWallBuilder;if(wall!=null)break;}
  if(wall==null)return;
  var a=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rainy VFX/Prefab/Particle System.prefab");
  var b=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rainy VFX/Prefab/Particle System (2).prefab");
  if(a==null||b==null)return;
  var go=new GameObject("Map Lightning System");Undo.RegisterCreatedObjectUndo(go,"Add map lightning");
  var controller=go.AddComponent<PolygonLightning>();controller.polygonWall=wall;controller.lightningPrefabs=new[]{a,b};
  EditorUtility.SetDirty(controller);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  Debug.Log("[LightningSetup] Registered both lightning prefabs and rain polygon in Stage_city.");
 }
}
