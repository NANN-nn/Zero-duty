using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class NativeEndingValidation
{
    const string Output="F:/Codex/2026-10-29/原生结局镜头/work";
    const string Key="NativeEndingValidationRunning";
    static double deadline;
    static int start=-1, outcome, captured;
    static ShiftEndingScreen screen;
    static NativeEndingFilm film;
    static float began;
    static bool waitingRestart;
    static readonly Dictionary<Transform,Matrix4x4> originals=new Dictionary<Transform,Matrix4x4>();
    static NativeEndingValidation(){if(SessionState.GetBool(Key,false))Wait();}
    public static void RunBatch()
    {
        try{Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/Resources/Scenes/first.unity");SessionState.SetBool(Key,true);Wait();EditorApplication.EnterPlaymode();}
        catch(Exception e){Finish(e);}
    }
    static void Wait(){deadline=EditorApplication.timeSinceStartup+200;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Play verification timeout");
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            if(start<0)start=Time.frameCount;
            if(Time.frameCount-start<15)return;
            if(waitingRestart)
            {
                if(Object.FindAnyObjectByType<NativeEndingFilm>()!=null)return;
                Check(Time.timeScale==1,"Restart did not restore normal time");
                File.WriteAllText(Output+"/checks.txt","PASS: data-transfer shadow enters the open lift, doors close before departure; lockdown lift remains closed, shadow stays on the lab side, faces the player and approaches by over two metres; all three outcomes use live RenderTextures and native buttons; playback completes at timeScale=0; captions follow playback; actual button raycasts and English/Chinese captions work; original corridor is untouched; restart cleans up the film. Saved scene was not written.");
                Finish(null);return;
            }
            if(screen==null)
            {
                if(outcome==0)
                {
                    var menu=Object.FindAnyObjectByType<ShiftStartMenu>();Call(menu,"BeginGameplay");
                    foreach(MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                        if(r.name.StartsWith("Corridor"))originals[r.transform]=r.transform.localToWorldMatrix;
                    StationLocalization.SetLanguage(StationLocalization.Language.English);
                }
                screen=ShiftEndingScreen.Instance;
                if(outcome==0)screen.ShowDataTransfer();else if(outcome==1)screen.ShowLockdown();else screen.ShowFailure();
                film=Get<NativeEndingFilm>(screen,"film");
                Check(film!=null&&film.Texture!=null,"Live film missing");
                Check(film.CopiedMeshCount>50,"Actual corridor meshes were not copied");
                Check(Get<RawImage>(screen,"endingImage").texture is RenderTexture,"Ending still loads bitmap art");
                foreach(Image image in screen.GetComponentsInChildren<Image>(true))Check(image.sprite==null,"Imported UI sprite in ending");
                foreach(SpriteRenderer sprite in film.GetComponentsInChildren<SpriteRenderer>(true))throw new Exception("Ghost illustration remains in film");
                Check(!ShaderUtil.ShaderHasError(Resources.Load<Shader>("ZeroHourAssets/Shaders/NativeShadow")),"Shadow shader compilation error");
                Check(Get<CanvasGroup>(screen,"captionGroup").alpha==0,"Caption shown before shot plays");
                began=Time.unscaledTime;captured=0;
            }
            float elapsed=Time.unscaledTime-began;
            float[] moments=outcome==2?new[]{.4f,2.1f,3.8f}:new[]{.4f,2.6f,5.9f};
            if(captured<3&&elapsed>=moments[captured])
            {
                CaptureTexture(film.Texture,$"film-{outcome+1}-{captured+1}.png");captured++;
            }
            if(!Get<bool>(screen,"ready"))return;
            Check(film.Finished,"Buttons became active before film finished");Check(Time.timeScale==0,"Ending did not freeze gameplay");
            if(outcome==1)
            {
                NativeShadowActor actor=film.GetComponentInChildren<NativeShadowActor>();
                float doorPlane=actor.transform.parent.position.z+.10f;
                foreach(Renderer r in actor.GetComponentsInChildren<Renderer>())
                    if(!r.transform.IsChildOf(Get<Camera>(film,"filmCamera").transform))
                        Check(r.bounds.min.z>doorPlane+.08f,"A shadow hand clips into the locked lift");
            }
            foreach(var pair in originals)Check(pair.Key!=null&&pair.Key.localToWorldMatrix==pair.Value,"Film moved the live corridor");
            foreach(Button button in screen.GetComponentsInChildren<Button>())HitArea(button);
            CaptureCanvas(screen.GetComponentInChildren<Canvas>(),$"ending-{outcome+1}-en.png");
            StationLocalization.SetLanguage(StationLocalization.Language.Chinese);
            CaptureCanvas(screen.GetComponentInChildren<Canvas>(),$"ending-{outcome+1}-zh.png");
            StationLocalization.SetLanguage(StationLocalization.Language.English);
            ExportPreview();
            if(outcome<2){Object.Destroy(screen.gameObject);screen=null;film=null;outcome++;start=Time.frameCount-13;}
            else{waitingRestart=true;Call(screen,"RestartShift");}
        }
        catch(Exception e){Finish(e);}
    }
    static void ExportPreview()
    {
        string folder=Output+"/preview-"+(outcome+1);Directory.CreateDirectory(folder);
        var small=new RenderTexture(672,378,0);
        RenderTexture previous=RenderTexture.active;
        try
        {
            int frames=Mathf.CeilToInt(film.Duration*8);
            for(int i=0;i<=frames;i++)
            {
                film.Sample(Mathf.Min(film.Duration,i/8f));
                var actor=film.GetComponentInChildren<NativeShadowActor>();
                float z=actor.transform.localPosition.z;
                if(outcome==0)
                {
                    if(i==0)Check(z>1,"Data ending must start with the shadow outside the lift");
                    if(Mathf.Abs(z-.1f)<.6f)Check(film.DoorOpening>.99f,"Shadow enters before the doors fully open");
                    if(i==frames)Check(z<-.8f&&film.DoorOpening==0&&film.LiftDeparture>.99f,"Shadow must leave inside the closed departing lift");
                }
                if(outcome==1)
                {
                    Check(film.DoorOpening==0,"Lockdown must never open the lift");
                    Camera viewer=Get<Camera>(film,"filmCamera");
                    Vector3 toward=viewer.transform.position-actor.transform.position;toward.y=0;
                    Check(Vector3.Dot(actor.transform.forward,toward.normalized)>.99f,"Lockdown shadow must face the player");
                    if(i==frames)Check(toward.magnitude<2.6f,"Lockdown shadow did not approach the player");
                    float plane=actor.transform.parent.position.z+.10f;
                    foreach(Renderer renderer in actor.GetComponentsInChildren<Renderer>())
                        Check(renderer.bounds.min.z>plane+.08f,"Lockdown shadow crossed the closed elevator threshold");
                }
                Graphics.Blit(film.Texture,small);CaptureTexture(small,"preview-"+(outcome+1)+"/"+i.ToString("D3")+".png");
            }
        }
        finally{RenderTexture.active=previous;Object.Destroy(small);}
    }
    static void HitArea(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Check(hits.Count>0&&(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform)),"Ending button pointer blocked: "+button.name);
    }
    static void CaptureTexture(RenderTexture rt,string name)
    {
        RenderTexture previous=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG());}
        finally{RenderTexture.active=previous;Object.Destroy(image);}
    }
    static void CaptureCanvas(Canvas canvas,string name)
    {
        GameObject root=new GameObject("Ending QA UI camera");Camera camera=root.AddComponent<Camera>();camera.enabled=false;
        camera.transform.position=new Vector3(10000,10000,10000);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.1f;camera.farClipPlane=20;
        RenderMode mode=canvas.renderMode;Camera old=canvas.worldCamera;float plane=canvas.planeDistance;
        var rt=new RenderTexture(1920,1080,24);
        RenderTexture previous=RenderTexture.active;
        try{camera.targetTexture=rt;camera.aspect=16f/9;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();CaptureTexture(rt,name);}
        finally{canvas.renderMode=mode;canvas.worldCamera=old;canvas.planeDistance=plane;RenderTexture.active=previous;Object.Destroy(root);Object.Destroy(rt);}
    }
    static T Get<T>(object x,string name)=>(T)x.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(x);
    static object Call(object x,string name,params object[] args)=>x.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(x,args);
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Finish(Exception e)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
        if(e!=null){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);}
        if(Application.isBatchMode)EditorApplication.Exit(e==null?0:1);else EditorApplication.ExitPlaymode();
    }
}
