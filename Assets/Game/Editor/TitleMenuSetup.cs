using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class TitleMenuSetup
{
    const string TitlePath="Assets/Game/S/Title2.unity";
    const string AssetPath="Assets/Game/UI/TitleMenu/";
    static TitleMenuSetup() { EditorApplication.delayCall+=Setup; }
    [MenuItem("Tools/Dead Shot/Setup New Title")]
    static void Setup()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(!File.Exists(AssetPath+"TitleBackground.mp4")) return;
        var previous=SceneManager.GetActiveScene();
        var scene=SceneManager.GetSceneByPath(TitlePath);
        bool opened=!scene.isLoaded;
        if(opened) scene=EditorSceneManager.OpenScene(TitlePath,OpenSceneMode.Additive);
        foreach(var c in Object.FindObjectsOfType<TitleMenuController>(true))
            if(c.gameObject.scene==scene) { if(opened) EditorSceneManager.CloseScene(scene,true); return; }
        var pause=Object.FindObjectOfType<PauseMenuController>(true);
        TMP_FontAsset font=pause!=null && pause.descriptionText!=null?pause.descriptionText.font:TMP_Settings.defaultFontAsset;
        if(font==null) { if(opened) EditorSceneManager.CloseScene(scene,true); return; }
        SceneManager.SetActiveScene(scene);
        foreach(var canvas in Object.FindObjectsOfType<Canvas>(true))
            if(canvas.gameObject.scene==scene) canvas.gameObject.SetActive(false);
        var import=AssetImporter.GetAtPath(AssetPath+"DeadShotLogo.png") as TextureImporter;
        if(import!=null) { import.textureType=TextureImporterType.Sprite; import.alphaIsTransparency=true; import.mipmapEnabled=false; import.SaveAndReimport(); }
        var root=new GameObject("Title Menu",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        var controller=root.AddComponent<TitleMenuController>();
        var bg=Rect("Video Background",root.transform,0,0,1920,1080);
        Stretch(bg); controller.background=bg.gameObject.AddComponent<RawImage>(); controller.background.color=Color.white; controller.background.raycastTarget=false;
        var vp=bg.gameObject.AddComponent<VideoPlayer>(); vp.playOnAwake=false; vp.isLooping=true; vp.audioOutputMode=VideoAudioOutputMode.None; vp.renderMode=VideoRenderMode.RenderTexture; vp.clip=AssetDatabase.LoadAssetAtPath<VideoClip>(AssetPath+"TitleBackground.mp4"); controller.video=vp;
        var gradient=new Texture2D(256,1,TextureFormat.RGBA32,false);
        for(int x=0;x<256;x++) gradient.SetPixel(x,0,new Color(0,0,0,.78f*Mathf.Pow(1-x/255f,2)));
        gradient.Apply(); File.WriteAllBytes(AssetPath+"MenuGradient.png",gradient.EncodeToPNG()); Object.DestroyImmediate(gradient); AssetDatabase.ImportAsset(AssetPath+"MenuGradient.png");
        var shade=Rect("Menu Shade",root.transform,0,0,1920,1080); Stretch(shade);
        var shadeImage=shade.gameObject.AddComponent<RawImage>(); shadeImage.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath+"MenuGradient.png"); shadeImage.raycastTarget=false;
        var menu=Rect("Main Menu",root.transform,0,0,1920,1080); Stretch(menu); controller.menu=menu.gameObject;
        var logo=Rect("Dead Shot Logo",menu,68,120,850,158).gameObject.AddComponent<Image>(); logo.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath+"DeadShotLogo.png"); logo.preserveAspect=true; logo.raycastTarget=false;
        Label(menu,"荒 廃 し た 都 市",font,230,282,530,46,28);
        controller.accent=Rect("Selection Accent",menu,90,430,620,110);
        var highlight=controller.accent.gameObject.AddComponent<Image>(); highlight.color=new Color(.13f,.16f,.18f,.65f); highlight.raycastTarget=false;
        var red=Rect("Red Line",controller.accent,0,0,5,110).gameObject.AddComponent<Image>(); red.color=new Color(.9f,.035f,.045f); red.raycastTarget=false;
        controller.rows=new RectTransform[3]; controller.labels=new TMP_Text[3];
        string[] en={"START","CONTINUE","OPTIONS"}, ja={"新しく始める","続きから","設定"};
        for(int i=0;i<3;i++) {
            var row=Rect(en[i],menu,90,430+i*145,620,110); controller.rows[i]=row;
            controller.labels[i]=Label(row,en[i],font,55,9,550,52,42);
            Label(row,ja[i],font,56,65,550,34,23);
        }
        Label(menu,"↑ ↓  選択    ホイール  選択    Enter / 左クリック  決定",font,90,990,1000,38,21);
        var settings=Rect("Title Key Settings",root.transform,0,0,1920,1080); Stretch(settings); settings.gameObject.SetActive(false);
        settings.gameObject.AddComponent<Image>().color=Color.black;
        var ui=settings.gameObject.AddComponent<KeyBindingSettingsUI>(); ui.font=font; ui.titleMenu=controller; ui.Build(); controller.settings=ui;
        controller.uiAudio=root.AddComponent<AudioSource>(); controller.uiAudio.playOnAwake=false; controller.uiAudio.ignoreListenerPause=true;
        if(pause!=null) { controller.moveSound=pause.selectionMoveSe; controller.confirmSound=pause.selectionConfirmSe; }
        bool hasEvents=false;
        foreach(var e in Object.FindObjectsOfType<EventSystem>(true)) if(e.gameObject.scene==scene) { e.gameObject.SetActive(true); hasEvents=true; }
        if(!hasEvents) new GameObject("Title EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("[TitleMenuSetup] Title2を保存: 動画・ロゴ・3項目・共通キー設定・Loading連携。");
        if(opened) { EditorSceneManager.CloseScene(scene,true); SceneManager.SetActiveScene(previous); }
    }
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var t=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); t.SetParent(parent,false);
        t.anchorMin=t.anchorMax=new Vector2(0,1); t.pivot=new Vector2(0,1); t.anchoredPosition=new Vector2(x,-y); t.sizeDelta=new Vector2(w,h); return t;
    }
    static void Stretch(RectTransform t) { t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=t.offsetMax=Vector2.zero; }
    static TMP_Text Label(Transform p,string value,TMP_FontAsset font,float x,float y,float w,float h,float size)
    { var text=Rect(value,p,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>(); text.font=font;text.text=value;text.fontSize=size;text.color=Color.white;text.enableWordWrapping=false;text.raycastTarget=false;return text; }
}
