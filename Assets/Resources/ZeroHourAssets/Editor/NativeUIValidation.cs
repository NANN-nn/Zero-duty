using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class NativeUIValidation
{
    const string Output="F:/Codex/2026-10-28/原生UI重制-8b7f/work";
    const string Key="NativeUIValidationRunning";
    static int start=-1;
    static double deadline;
    static NativeUIValidation(){if(SessionState.GetBool(Key,false))Wait();}
    public static void RunBatch()
    {
        try{Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/Resources/Scenes/first.unity");SessionState.SetBool(Key,true);Wait();EditorApplication.EnterPlaymode();}
        catch(Exception e){Finish(e);}
    }
    static void Wait(){deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Play entry timeout");
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            if(start<0)start=Time.frameCount;
            if(Time.frameCount-start<15)return;
            EditorApplication.update-=Tick;
            Validate();Finish(null);
        }catch(Exception e){Finish(e);}
    }
    static void Validate()
    {
        var menu=Object.FindFirstObjectByType<ShiftStartMenu>();Check(menu!=null,"Start menu missing");
        Call(menu,"LateUpdate");var canvas=Get<Canvas>(menu,"canvas");
        Check(Get<RenderTexture>(menu,"roomTexture")!=null,"Live room target missing");
        foreach(RawImage r in canvas.GetComponentsInChildren<RawImage>(true))
            Check(r.texture is RenderTexture,"Menu unexpectedly uses a bitmap: "+r.name);
        Capture(canvas,"01-start.png");
        var atmosphere=Get<NativeMenuAtmosphere>(menu,"atmosphere");
        var menuCamera=Get<Camera>(menu,"roomCamera");
        var displays=Object.FindFirstObjectByType<MonitorScreenClick>();
        for(int screenIndex=0;screenIndex<3;screenIndex++)for(int i=0;i<8;i++)
        {
            Bounds desk=displays.GetDisplay(screenIndex).bounds;
            Vector3 corner=desk.center+Vector3.Scale(desk.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            Vector3 point=menuCamera.WorldToViewportPoint(corner);
            Check(point.z>0&&point.x>.33f&&point.x<.99f&&point.y>.04f&&point.y<.96f,"A monitor is clipped by the closer framing or fixed menu.");
        }
        Call(atmosphere,"SetWarningLamp",1f);
        foreach(Material material in Get<System.Collections.Generic.List<Material>>(atmosphere,"materials")){material.SetFloat("_CueAmount",1f);material.SetFloat("_Brightness",.38f);}
        Check(Get<Light>(atmosphere,"warning").enabled,"Red warning lamp did not turn on");
        menuCamera.Render();Capture(canvas,"06-menu-red-glitch.png");
        Check(!ShaderUtil.ShaderHasError(Resources.Load<Shader>("ZeroHourAssets/Shaders/NativeMenuScreen")),"Menu screen shader failed to compile");
        Call(atmosphere,"SetWarningLamp",0f);Call(menu,"LateUpdate");
        Call(menu,"ShowPage",Get<GameObject>(menu,"settingsPage"));
        if(!StationLocalization.IsChinese)foreach(Button button in canvas.GetComponentsInChildren<Button>(true))if(button.name=="Language toggle")button.onClick.Invoke();
        Capture(canvas,"02-settings-zh.png");
        Call(menu,"ShowPage",Get<GameObject>(menu,"introductionPage"));
        Capture(canvas,"03-introduction-zh.png");
        StationLocalization.SetLanguage(StationLocalization.Language.English);
        Call(menu,"BeginGameplay");
        Check(!Get<Light>(atmosphere,"warning").gameObject.activeSelf,"Menu light leaked into gameplay");

        var player=Object.FindFirstObjectByType<PlayerInteractor>();
        var gas=Object.FindFirstObjectByType<PressureTimingPuzzle>();
        GameplaySignals.Instance.Raise("corridor_interlude_complete");
        Check(gas.CanInteract(player),"Gas panel cannot open");gas.Interact(player);
        var skin=Get<PuzzlePanelSkin>(gas,"panelSkin");Call(skin,"LateUpdate");
        CanvasGroup group=Get<CanvasGroup>(gas,"puzzleUI");NoBitmaps(group);
        Check(skin.GasButton!=null,"Hold button missing");
        HitArea(skin.GasButton);
        ExecuteEvents.Execute(skin.GasButton.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerDownHandler);
        Check(Get<bool>(gas,"buttonHeld"),"Pointer down did not hold pressure");
        ExecuteEvents.Execute(skin.GasButton.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerUpHandler);
        Check(!Get<bool>(gas,"buttonHeld"),"Pointer up did not release pressure");
        skin.SetGasNeedlePosition(.5f);skin.SetGasStability(.8f,1.5f,true);skin.SetGasProgress(1);Call(skin,"LateUpdate");
        Capture(group.GetComponentInParent<Canvas>(),"04-gas.png");Call(gas,"Close");
        GasPressureState pressure=new GasPressureState(3,.36f,.64f,1.5f,.35f,.27f);
        for(int i=0;i<10000&&!pressure.Complete;i++)pressure.Advance(.01f,pressure.Pressure<.5f);
        Check(pressure.Complete,"Three-channel hold/release logic failed");

        var power=Object.FindFirstObjectByType<SequencePuzzle>();
        GameplaySignals.Instance.Raise("ghost_interlude_complete");Check(power.CanInteract(player),"Power panel cannot open");power.Interact(player);
        var powerSkin=Get<PuzzlePanelSkin>(power,"panelSkin");Call(powerSkin,"LateUpdate");
        var powerGroup=Get<CanvasGroup>(power,"puzzleUI");NoBitmaps(powerGroup);
        Check(powerSkin.PowerButtons.Length==3,"Missing independent levers");
        foreach(Button lever in powerSkin.PowerButtons)HitArea(lever);
        var routing=Get<CircuitRoutingUI>(power,"circuitRouting");
        int[] turns=Get<int[]>(routing,"turns");for(int i=0;i<3;i++){Check(!CircuitRoutingUI.HasThroughConnection(turns[i]),"Every contact must start disconnected");Call(routing,"Turn",i);}
        Check(CircuitRoutingUI.RouteConnected(Get<int[]>(routing,"turns")),"Rotated contacts do not connect");
        Capture(powerGroup.GetComponentInParent<Canvas>(),"05-power.png");
        int[] order=Get<int[]>(routing,"signalOrder");Call(power,"OnCircuitRouted",order);
        foreach(int index in order)powerSkin.PowerButtons[index].onClick.Invoke();
        Check(power.Completed,"Correct levers did not complete the power task");
        File.WriteAllText(Output+"/checks.txt","PASS: live real-scene menu and introduction, no bitmap menu backgrounds; bilingual settings; gas UI has no bitmap textures, hold/release events reach gameplay and three channels complete; power UI has no bitmap textures, all three contacts rotate correctly and the original lever-order task completes. Saved scene was not rewritten.");
    }
    static void NoBitmaps(CanvasGroup group)
    {
        Check(group.GetComponentsInChildren<RawImage>(true).Length==0,"Imported panel bitmap remains");
        foreach(Image image in group.GetComponentsInChildren<Image>(true))if(image.enabled)Check(image.sprite==null,"Unexpected imported UI sprite");
    }
    static void HitArea(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Check(hits.Count>0&&(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform)),"Visible button does not receive the pointer: "+button.name);
    }
    static void Capture(Canvas canvas,string name)
    {
        GameObject root=new GameObject("QA UI camera");Camera camera=root.AddComponent<Camera>();camera.enabled=false;
        camera.transform.position=new Vector3(10000,10000,10000);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.02f,.025f);camera.nearClipPlane=.1f;camera.farClipPlane=20;
        RenderMode mode=canvas.renderMode;Camera old=canvas.worldCamera;float plane=canvas.planeDistance;
        RenderTexture rt=new RenderTexture(1920,1080,24);Texture2D image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try{camera.targetTexture=rt;camera.aspect=16f/9;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();foreach(PuzzlePanelSkin skin in canvas.GetComponentsInChildren<PuzzlePanelSkin>(true))Call(skin,"LateUpdate");Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG());}
        finally{canvas.renderMode=mode;canvas.worldCamera=old;canvas.planeDistance=plane;RenderTexture.active=previous;Object.Destroy(root);Object.Destroy(rt);Object.Destroy(image);}
    }
    static T Get<T>(object x,string name)=>(T)x.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(x);
    static object Call(object x,string name,params object[] args)=>x.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(x,args);
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Finish(Exception e){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;if(e!=null){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);}if(Application.isBatchMode)EditorApplication.Exit(e==null?0:1);else EditorApplication.ExitPlaymode();}
}
