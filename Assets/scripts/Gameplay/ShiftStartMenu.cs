using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>Native text/buttons over a live view of the actual control room.</summary>
public sealed class ShiftStartMenu : MonoBehaviour
{
    private const string VolumeKey="ZeroDuty.NativeUI.MasterVolume";
    private static ShiftStartMenu instance;
    private Canvas canvas;
    private GameObject mainPage,settingsPage,introductionPage;
    private RawImage backdrop,introductionView;
    private Camera roomCamera;
    private RenderTexture roomTexture;
    private Camera introductionCamera;
    private RenderTexture introductionTexture;
    private MonitorCameraController menuFeeds;
    private float nextFeedRefresh;
    private NativeMenuAtmosphere atmosphere;
    private bool visible,startedThisRun;
    private TMP_Text volumeValue,languageValue;
    private Transform chair;
    private Quaternion chairRotation;
    private readonly Dictionary<PlayerInteractor,bool> interactors=new Dictionary<PlayerInteractor,bool>();
    private readonly Dictionary<playerController,bool> players=new Dictionary<playerController,bool>();
    private readonly Dictionary<FirstPersonCameraController,bool> looks=new Dictionary<FirstPersonCameraController,bool>();
    public static bool ShiftStarted=>instance!=null&&instance.startedThisRun;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()=>instance=null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create(){if(instance!=null)return;var root=new GameObject("Native Start Menu");DontDestroyOnLoad(root);root.AddComponent<ShiftStartMenu>();}
    private void Awake(){if(instance!=null){Destroy(gameObject);return;}instance=this;BuildUI();canvas.enabled=false;SceneManager.sceneLoaded+=OnSceneLoaded;}
    private void OnSceneLoaded(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="first"||startedThisRun)return;
        BuildRoomCamera();visible=true;canvas.enabled=true;ShowPage(mainPage);Time.timeScale=0;
        foreach(PlayerInteractor p in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None)){interactors[p]=p.InputEnabled;p.InputEnabled=false;}
        foreach(playerController p in FindObjectsByType<playerController>(FindObjectsSortMode.None)){players[p]=p.InputEnabled;p.InputEnabled=false;}
        foreach(FirstPersonCameraController p in FindObjectsByType<FirstPersonCameraController>(FindObjectsSortMode.None)){looks[p]=p.InputEnabled;p.InputEnabled=false;}
        AudioListener.volume=PlayerPrefs.GetFloat(VolumeKey,1f);
    }
    private void BuildRoomCamera()
    {
        if(introductionCamera!=null)Destroy(introductionCamera.gameObject);
        if(introductionTexture!=null){introductionTexture.Release();Destroy(introductionTexture);}
        if(roomCamera!=null)Destroy(roomCamera.gameObject);
        if(roomTexture!=null){roomTexture.Release();Destroy(roomTexture);}
        var root=new GameObject("Native Menu Room Camera");root.transform.SetParent(transform,false);
        roomCamera=root.AddComponent<Camera>();
        Camera player=Camera.main;if(player!=null)roomCamera.CopyFrom(player);
        roomCamera.enabled=false;roomCamera.nearClipPlane=.03f;roomCamera.fieldOfView=68f;
        roomCamera.cullingMask &= ~(1<<31);
        MonitorViewManager monitor=FindFirstObjectByType<MonitorViewManager>();
        menuFeeds=FindFirstObjectByType<MonitorCameraController>();
        Transform seat=monitor!=null?monitor.OverviewPose:GameObject.Find("SeatedCamera")?.transform;
        if(seat!=null)
        {
            Vector3 direction=Vector3.right;
            roomCamera.transform.position=seat.position-direction*.65f+Vector3.up*.15f;
            roomCamera.transform.LookAt(seat.position+direction*3f);
        }
        else if(player!=null)root.transform.SetPositionAndRotation(player.transform.position,player.transform.rotation);
        atmosphere=root.AddComponent<NativeMenuAtmosphere>();
        atmosphere.Initialize(roomCamera,menuFeeds);
        roomTexture=new RenderTexture(1920,1080,24){name="Control room live view"};roomTexture.Create();roomCamera.targetTexture=roomTexture;
        backdrop.texture=roomTexture;
        introductionCamera=new GameObject("Introduction frontal desk camera").AddComponent<Camera>();
        introductionCamera.transform.SetParent(transform,false);introductionCamera.CopyFrom(roomCamera);introductionCamera.enabled=false;
        NativeMenuAtmosphere.FrameIntroduction(introductionCamera,FindAnyObjectByType<MonitorScreenClick>());
        introductionTexture=new RenderTexture(1920,1080,24){name="Introduction frontal control desk"};introductionTexture.Create();
        introductionCamera.targetTexture=introductionTexture;introductionView.texture=introductionTexture;
        foreach(Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if(r.name.ToLowerInvariant().Contains("chair")&&seat!=null&&Vector3.Distance(r.transform.position,seat.position)<3f)
            {chair=r.transform;chairRotation=chair.localRotation;break;}
    }
    private void Update()
    {
        if(!visible)return;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)ShowPage(mainPage);
    }
    private void LateUpdate()
    {
        if(!visible||roomCamera==null)return;
        if(chair!=null)chair.localRotation=chairRotation*Quaternion.Euler(0,Mathf.Sin(Time.unscaledTime*.65f)*.7f,0);
        // Brightness varies subtly; the camera, room and UI never drift.
        float pulse=1f-(Mathf.Sin(Time.unscaledTime*1.3f)>.985f?.07f:0f);
        backdrop.color=new Color(pulse,pulse,pulse);
        if(Time.unscaledTime>=nextFeedRefresh&&menuFeeds!=null){for(int i=0;i<3;i++){Camera feed=menuFeeds.GetCamera(i);if(feed!=null)feed.Render();}nextFeedRefresh=Time.unscaledTime+.2f;}
        atmosphere?.Refresh(Time.unscaledTime);
        roomCamera.Render();
        if(introductionPage.activeSelf&&introductionCamera!=null)introductionCamera.Render();
    }
    private void StartShift()=>ShowPage(introductionPage);
    private void BeginGameplay()
    {
        if(!visible)return;visible=false;startedThisRun=true;canvas.enabled=false;Time.timeScale=1;
        if(atmosphere!=null)atmosphere.Release();
        if(chair!=null)chair.localRotation=chairRotation;
        foreach(var p in interactors)if(p.Key!=null)p.Key.InputEnabled=p.Value;
        foreach(var p in players)if(p.Key!=null)p.Key.InputEnabled=p.Value;
        foreach(var p in looks)if(p.Key!=null)p.Key.InputEnabled=p.Value;
        ShiftAudioDirector.BeginShift();Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
    }
    private void ExitGame(){Time.timeScale=1;PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
    private void ShowPage(GameObject page){mainPage.SetActive(page==mainPage);settingsPage.SetActive(page==settingsPage);introductionPage.SetActive(page==introductionPage);}
    private void BuildUI()
    {
        var root=new GameObject("Native Start Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
        canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=4000;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        backdrop=Rect("Live room",root.transform,Vector2.zero,new Vector2(1920,1080)).gameObject.AddComponent<RawImage>();backdrop.raycastTarget=false;
        var shade=Image("Menu left panel",root.transform,new Vector2(-645,0),new Vector2(630,1080),new Color(.018f,.027f,.033f,.97f));
        Image("Panel edge",root.transform,new Vector2(-330,0),new Vector2(2,1080),new Color(.24f,.34f,.37f));
        Image("Top status light",root.transform,new Vector2(-848,419),new Vector2(40,5),new Color(.42f,.72f,.73f));
        mainPage=Page("Main",root.transform);settingsPage=Page("Settings",root.transform);introductionPage=Page("Introduction",root.transform);
        Text("Title",mainPage.transform,"END OF SHIFT",69,new Vector2(-645,255),new Vector2(525,160));
        Text("Tagline",mainPage.transform,"Just another quiet night",26,new Vector2(-645,145),new Vector2(510,80));
        Button("Start",mainPage.transform,"START",-10,StartShift);
        Button("Settings",mainPage.transform,"SETTINGS",-120,()=>ShowPage(settingsPage));
        Button("Exit",mainPage.transform,"EXIT GAME",-230,ExitGame);
        Text("Settings title",settingsPage.transform,"SETTINGS",55,new Vector2(-645,310),new Vector2(510,100));
        Text("Volume",settingsPage.transform,"MASTER VOLUME",25,new Vector2(-645,188),new Vector2(510,60));
        RectTransform sliderRect=Rect("Volume slider",settingsPage.transform,new Vector2(-645,100),new Vector2(420,44));
        Slider slider=sliderRect.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;
        Image("Track",sliderRect,Vector2.zero,new Vector2(420,6),new Color(.16f,.23f,.26f));
        Image handle=Image("Handle",sliderRect,Vector2.zero,new Vector2(18,36),new Color(.61f,.74f,.75f));slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
        volumeValue=Text("Volume value",settingsPage.transform,"100%",24,new Vector2(-645,35),new Vector2(510,44));
        slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(VolumeKey,1));
        slider.onValueChanged.AddListener(v=>{AudioListener.volume=v;PlayerPrefs.SetFloat(VolumeKey,v);StationLocalization.Set(volumeValue,Mathf.RoundToInt(v*100)+"%");});
        StationLocalization.Set(volumeValue,Mathf.RoundToInt(slider.value*100)+"%");
        Text("Language",settingsPage.transform,"LANGUAGE",24,new Vector2(-645,-50),new Vector2(510,44));
        var language=Button("Language toggle",settingsPage.transform,LanguageCaption(),-130,()=>{StationLocalization.SetLanguage(StationLocalization.IsChinese?StationLocalization.Language.English:StationLocalization.Language.Chinese);StationLocalization.Set(languageValue,LanguageCaption());});
        languageValue=language.GetComponentInChildren<TMP_Text>();
        Button("Back",settingsPage.transform,"BACK",-250,()=>ShowPage(mainPage));
        Image("Introduction backing",introductionPage.transform,Vector2.zero,new Vector2(1920,1080),new Color(.012f,.018f,.024f));
        introductionView=Rect("Real control room introduction",introductionPage.transform,new Vector2(-335,0),new Vector2(1120,630)).gameObject.AddComponent<RawImage>();introductionView.raycastTarget=false;
        Text("Intro title",introductionPage.transform,"NIGHT SHIFT",58,new Vector2(650,290),new Vector2(490,100));
        var story=Text("Intro story",introductionPage.transform,"The last shift has gone home.\n\nYou're on your own down here until morning.\n\nThey said it would be a quiet night.",32,new Vector2(650,0),new Vector2(460,410));story.alignment=TextAlignmentOptions.Left;
        var next=Button("Continue",introductionPage.transform,"CONTINUE",-320,BeginGameplay);next.GetComponent<RectTransform>().anchoredPosition=new Vector2(650,-320);
    }
    private static string LanguageCaption()=>StationLocalization.IsChinese?"<  简体中文  >":"<  English  >";
    private static GameObject Page(string name,Transform parent){var r=Rect(name,parent,Vector2.zero,new Vector2(1920,1080));return r.gameObject;}
    private static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
    private static Image Image(string name,Transform parent,Vector2 pos,Vector2 size,Color color){var g=Rect(name,parent,pos,size).gameObject.AddComponent<Image>();g.color=color;g.raycastTarget=false;return g;}
    private static TextMeshProUGUI Text(string name,Transform parent,string content,float size,Vector2 pos,Vector2 dimensions){var t=Rect(name,parent,pos,dimensions).gameObject.AddComponent<TextMeshProUGUI>();t.font=StationTypography.UIAsset;StationLocalization.Set(t,content);t.fontSize=size;t.fontStyle=FontStyles.Bold;t.alignment=TextAlignmentOptions.Center;t.color=new Color(.8f,.87f,.88f);t.raycastTarget=false;return t;}
    private static Button Button(string name,Transform parent,string caption,float y,UnityEngine.Events.UnityAction action)
    {
        var r=Rect(name,parent,new Vector2(-645,y),new Vector2(440,78));var face=r.gameObject.AddComponent<NativePanelGraphic>();face.color=new Color(.065f,.09f,.105f);face.raycastTarget=true;
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=face;b.navigation=new Navigation{mode=Navigation.Mode.None};
        var c=b.colors;c.normalColor=Color.white;c.highlightedColor=new Color(1.7f,1.9f,2);c.pressedColor=new Color(.6f,.7f,.75f);c.selectedColor=Color.white;b.colors=c;
        Text("Caption",r,caption,29,Vector2.zero,new Vector2(410,68));b.onClick.AddListener(()=>{ShiftAudioDirector.PlayUiConfirm();action();});return b;
    }
    private void OnDestroy(){SceneManager.sceneLoaded-=OnSceneLoaded;if(chair!=null)chair.localRotation=chairRotation;if(introductionCamera!=null)introductionCamera.targetTexture=null;if(introductionTexture!=null){introductionTexture.Release();Destroy(introductionTexture);}if(roomTexture!=null){roomTexture.Release();Destroy(roomTexture);}if(visible)Time.timeScale=1;}
}
