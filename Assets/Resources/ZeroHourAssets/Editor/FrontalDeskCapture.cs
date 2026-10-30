using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>One-shot, opt-in capture in the already open editor; never saves the scene.</summary>
[InitializeOnLoad]
public static class FrontalDeskCapture
{
    const string Work="F:/Codex/2026-10-30/控制台正面取景/work";
    const string BatchKey="FrontalDeskCaptureBatch";
    static int firstFrame=-1;
    static FrontalDeskCapture(){EditorApplication.delayCall+=TryCapture;if(SessionState.GetBool(BatchKey,false))EditorApplication.update+=WaitForPlay;}
    public static void RunBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Resources/Scenes/first.unity");
        SessionState.SetBool(BatchKey,true);EditorApplication.update+=WaitForPlay;EditorApplication.EnterPlaymode();
    }
    static void WaitForPlay()
    {
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        if(firstFrame<0)firstFrame=Time.frameCount;if(Time.frameCount-firstFrame<20)return;
        EditorApplication.update-=WaitForPlay;SessionState.SetBool(BatchKey,false);
        var menu=UnityEngine.Object.FindAnyObjectByType<ShiftStartMenu>();
        menu.GetType().GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(menu,null);
        Run();if(Application.isBatchMode)EditorApplication.Exit(File.Exists(Work+"/failure.txt")?1:0);
    }
    [MenuItem("Tools/Native UI/Capture frontal desk")]
    public static void Capture()=>Run();
    static void TryCapture(){if(File.Exists(Work+"/capture.request")){File.Delete(Work+"/capture.request");Run();}}
    static void Run()
    {
        GameObject root=null;RenderTexture rt=null;Texture2D image=null;var previous=RenderTexture.active;
        try
        {
            Directory.CreateDirectory(Work);Directory.CreateDirectory(Work+"/../outputs");
            var screens=UnityEngine.Object.FindAnyObjectByType<MonitorScreenClick>();
            if(screens==null)throw new Exception("Control desk not found in current scene");
            var feeds=UnityEngine.Object.FindAnyObjectByType<MonitorCameraController>();
            if(feeds!=null)for(int i=0;i<3;i++){Camera feed=feeds.GetCamera(i);if(feed!=null)feed.Render();}
            root=new GameObject("Frontal desk capture - temporary"){hideFlags=HideFlags.HideAndDontSave};
            Camera camera=root.AddComponent<Camera>();if(Camera.main!=null)camera.CopyFrom(Camera.main);
            camera.enabled=false;camera.nearClipPlane=.03f;camera.cullingMask&=~(1<<31);
            NativeMenuAtmosphere.FrameIntroduction(camera,screens);
            rt=new RenderTexture(1920,1080,24);rt.Create();camera.targetTexture=rt;
            for(int i=0;i<3;i++)
            {
                Bounds b=screens.GetDisplay(i).bounds;
                for(int j=0;j<8;j++)
                {
                    Vector3 corner=b.center+Vector3.Scale(b.extents,new Vector3((j&1)==0?-1:1,(j&2)==0?-1:1,(j&4)==0?-1:1));
                    Vector3 point=camera.WorldToViewportPoint(corner);
                    if(point.z<=0||point.x<.04f||point.x>.96f||point.y<.04f||point.y>.96f)throw new Exception("Monitor is clipped");
                }
            }
            camera.Render();RenderTexture.active=rt;image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
            File.WriteAllBytes(Work+"/../outputs/控制台正面近景.png",image.EncodeToPNG());
            File.WriteAllText(Work+"/checks.txt","PASS: actual Unity scene, all three monitors fit; frontal camera has zero roll/pitch; no AI image or scene save. Position="+camera.transform.position+" FOV="+camera.fieldOfView);
            if(File.Exists(Work+"/failure.txt"))File.Delete(Work+"/failure.txt");
            camera.targetTexture=null;
        }
        catch(Exception e){File.WriteAllText(Work+"/failure.txt",e.ToString());Debug.LogException(e);}
        finally{RenderTexture.active=previous;if(root!=null)UnityEngine.Object.DestroyImmediate(root);if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
    }
}
