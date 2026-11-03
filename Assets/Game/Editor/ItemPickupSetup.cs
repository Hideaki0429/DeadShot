using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class ItemPickupSetup
{
 static ItemPickupSetup(){EditorApplication.delayCall+=Setup;}
 static void EnsureTag(){var asset=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");var so=new SerializedObject(asset[0]);var tags=so.FindProperty("tags");for(int i=0;i<tags.arraySize;i++)if(tags.GetArrayElementAtIndex(i).stringValue=="Item")return;tags.InsertArrayElementAtIndex(tags.arraySize);tags.GetArrayElementAtIndex(tags.arraySize-1).stringValue="Item";so.ApplyModifiedProperties();}
 static void Setup(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)return;
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="Stage_city")return;
  EnsureTag();var player=Object.FindObjectOfType<UnityChan.UnityChanControlScriptWithRgidBody>(true);if(player==null||player.gameObject.scene!=scene)return;
  if(player.GetComponent<PlayerItemCollector>()!=null)return;
  var collector=Undo.AddComponent<PlayerItemCollector>(player.gameObject);
  collector.playerAnimator=player.GetComponent<Animator>();collector.playerHealth=player.GetComponent<PlayerHealth>();
  if(collector.playerHealth==null)collector.playerHealth=Object.FindObjectOfType<PlayerHealth>(true);
  player.gameObject.tag="Player";PrefabUtility.RecordPrefabInstancePropertyModifications(player.gameObject);
  EditorUtility.SetDirty(collector);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  Debug.Log("[ItemPickupSetup] Collector registered; health="+(collector.playerHealth!=null));
 }
 [MenuItem("Tools/Dead Shot/Prepare Selected Item Pickups")]
 static void Prepare(){EnsureTag();foreach(var go in Selection.gameObjects){
  if(EditorUtility.IsPersistent(go))continue;
  Undo.RecordObject(go,"Set Item tag");go.tag="Item";
  var pickup=go.GetComponent<InventoryPickup>();if(pickup==null)pickup=Undo.AddComponent<InventoryPickup>(go);
  var sphere=pickup.Trigger;Undo.RecordObject(sphere,"Item trigger");sphere.isTrigger=true;
  if(go.GetComponentInParent<Rigidbody>()==null){var rb=Undo.AddComponent<Rigidbody>(go);rb.isKinematic=true;rb.useGravity=false;}
  EditorUtility.SetDirty(pickup);EditorSceneManager.MarkSceneDirty(go.scene);
 }Debug.Log("[ItemPickupSetup] Set Item ID / Amount / Hold Seconds / Pickup SE on each selected item. Save scene after configuration.");}
}
