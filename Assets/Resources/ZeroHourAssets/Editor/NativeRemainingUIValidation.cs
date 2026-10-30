using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class NativeRemainingUIValidation
{
    const string Output="F:/Codex/2026-10-30/其余原生UI替换/work";
    const string Key="NativeRemainingUIValidationRunning";
    static int start=-1;static double deadline;
    static NativeRemainingUIValidation(){if(SessionState.GetBool(Key,false))Wait();}
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
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Play timeout");
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            if(start<0)start=Time.frameCount;if(Time.frameCount-start<20)return;
            EditorApplication.update-=Tick;Validate();Finish(null);
        }
        catch(Exception e){Finish(e);}
    }
    static void Validate()
    {
        var menu=Object.FindAnyObjectByType<ShiftStartMenu>();Call(menu,"BeginGameplay");
        StationLocalization.SetLanguage(StationLocalization.Language.English);
        var player=Object.FindAnyObjectByType<PlayerInteractor>();
        var hud=Object.FindAnyObjectByType<StationObjectiveAndPauseUI>();
        Call(hud,"Pause");Check(Time.timeScale==0,"Pause did not stop gameplay");
        var pause=Get<GameObject>(hud,"pausePage");var audio=Get<GameObject>(hud,"audioPage");
        Canvas canvas=pause.GetComponentInParent<Canvas>();NoArt(canvas.gameObject);
        Capture(canvas,"01-pause-en.png");
        foreach(Button b in pause.GetComponentsInChildren<Button>())Hit(b);
        Capture(canvas,"01-pause-en.png");
        FindButton(pause,"Next Page").onClick.Invoke();Check(audio.activeSelf&&!pause.activeSelf,"Page arrow did not switch to audio");
        Slider slider=audio.GetComponentInChildren<Slider>();float saved=AudioListener.volume;
        slider.value=.63f;Check(Mathf.Abs(AudioListener.volume-.63f)<.001f,"Volume did not change");
        Check(Get<TMPro.TextMeshProUGUI>(hud,"volumeValueText").text=="63%","Volume label did not update");
        Capture(canvas,"02-audio-en.png");Hit(slider.gameObject);slider.value=saved;
        StationLocalization.SetLanguage(StationLocalization.Language.Chinese);Capture(canvas,"02-audio-zh.png");
        FindButton(audio,"Previous Page").onClick.Invoke();Capture(canvas,"01-pause-zh.png");
        FindButton(pause,"Resume").onClick.Invoke();Check(Time.timeScale==1&&player.InputEnabled,"Resume did not restore gameplay input");

        var activities=OptionalPropActivities.Instance;
        var savedLanguage=StationLocalization.Current;
        var captured=new HashSet<string>();int noteCount=0;
        foreach(var language in new[]{StationLocalization.Language.English,StationLocalization.Language.Chinese})
        {
            StationLocalization.SetLanguage(language);
            foreach(OptionalPropInteraction note in Object.FindObjectsByType<OptionalPropInteraction>(FindObjectsSortMode.None))
            {
                if(!note.enabled||note.Kind!=OptionalPropInteraction.PropKind.Note||note.name.StartsWith("StickyNotes_Stack"))continue;
                note.EnsureNoteContent();activities.Read(note,player);Canvas.ForceUpdateCanvases();
                GameObject panel=Get<GameObject>(activities,"notePanel");NoArt(panel);
                Check(panel.GetComponentInChildren<NativePaperGraphic>()!=null,"Paper mesh missing");
                Text body=Get<Text>(activities,"noteBody");Check((bool)Call(activities,"FitsHandbookPage",body),"Text overflows "+note.name+" / "+language);
                if(note.NoteStyle==OptionalPropInteraction.NotePaperStyle.Book)
                    Check((bool)Call(activities,"FitsHandbookPage",Get<Text>(activities,"noteRightBody")),"Right page overflows "+note.name);
                string name=note.name=="ShiftLog_Open1"?"handbook":note.NoteStyle.ToString().ToLowerInvariant();
                string key=name+"-"+(language==StationLocalization.Language.English?"en":"zh");
                if(captured.Add(key))Capture(panel.GetComponentInParent<Canvas>(),"03-note-"+key+".png");
                Button close=Get<RectTransform>(activities,"noteCloseRect").GetComponent<Button>();Hit(close);close.onClick.Invoke();
                Check(!OptionalPropActivities.ReadingNote&&player.InputEnabled,"Note close did not restore input");noteCount++;
            }
        }
        Check(noteCount>=18,"Placed notes were not found");
        GameplaySignals.Instance.Raise("final_protocol_unlocked");
        var terminal=Object.FindAnyObjectByType<FinalChoiceTerminal>();
        Check(terminal.CanInteract(player),"Final controller did not unlock");terminal.Interact(player);
        Canvas final=Get<GameObject>(terminal,"runtimeCanvas").GetComponent<Canvas>();NoArt(final.gameObject);
        Capture(final,"04-final-controller-en.png");
        Button transfer=Get<Button>(terminal,"transferButton"),lockdown=Get<Button>(terminal,"lockdownButton");
        Hit(transfer);Hit(lockdown);
        foreach(var language in new[]{StationLocalization.Language.English,StationLocalization.Language.Chinese})
        {
            StationLocalization.SetLanguage(language);
            ExecuteEvents.Execute(transfer.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
            Check(transfer.GetComponentInChildren<NativeButtonFeedback>()!=null,"Hover feedback missing");
            Capture(final,"04-final-controller-"+(language==StationLocalization.Language.English?"en":"zh")+".png");
            ExecuteEvents.Execute(transfer.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
        }
        lockdown.onClick.Invoke();Check(terminal.ChoiceMade&&GameplaySignals.Instance.HasRaised("ending_facility_lockdown"),"Native lockdown button did not choose the ending");
        var film=Object.FindAnyObjectByType<NativeEndingFilm>();Check(film!=null,"Ending film was not started by controller");
        Object.DestroyImmediate(ShiftEndingScreen.Instance.gameObject);
        // A fresh terminal verifies the second independent action without reusing ChoiceMade state.
        var testRoot=new GameObject("QA second final choice");var second=testRoot.AddComponent<FinalChoiceTerminal>();
        Check(second.CanInteract(player),"Second test terminal did not unlock");second.Interact(player);
        Get<Button>(second,"transferButton").onClick.Invoke();Check(GameplaySignals.Instance.HasRaised("ending_data_transfer"),"Native transfer button did not choose the ending");
        StationLocalization.SetLanguage(savedLanguage);
        File.WriteAllText(Output+"/checks.txt","PASS: native pause/audio pages have no imported artwork; arrows switch pages; volume slider changes AudioListener and its label; resume restores time and input; "+noteCount+" note/language combinations fit, preserve both book pages, and close correctly; final controller has no imported artwork, both independent hit areas work and both actual ending actions run; localized captures exported. Saved scene was not written.");
    }
    static Button FindButton(GameObject root,string name){foreach(Button b in root.GetComponentsInChildren<Button>(true))if(b.name==name)return b;throw new Exception("Missing button "+name);}
    static void NoArt(GameObject root)
    {
        Check(root.GetComponentsInChildren<RawImage>(true).Length==0,"Imported RawImage remains in "+root.name);
        foreach(Image image in root.GetComponentsInChildren<Image>(true))Check(image.sprite==null,"Imported sprite remains in "+image.name);
    }
    static void Hit(Button button)=>Hit(button.gameObject);
    static void Hit(GameObject target)
    {
        Canvas.ForceUpdateCanvases();var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,target.transform.position)};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
        string details="";foreach(var hit in hits)details+=" / "+hit.gameObject.name+" on "+hit.gameObject.GetComponentInParent<Canvas>().name;
        Check(hits.Count>0&&(hits[0].gameObject==target||hits[0].gameObject.transform.IsChildOf(target.transform)),"Pointer blocked: "+target.name+" at "+e.position+details);
    }
    static void Capture(Canvas canvas,string name)
    {
        var go=new GameObject("UI validation camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(10000,10000,10000);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.025f,.033f);camera.farClipPlane=20;camera.nearClipPlane=.1f;
        var rt=new RenderTexture(1920,1080,24);var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldPlane=canvas.planeDistance;var previous=RenderTexture.active;
        try{camera.targetTexture=rt;camera.aspect=16f/9;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG());}
        finally{canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldPlane;RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(go);Object.Destroy(rt);Object.Destroy(image);}
    }
    static T Get<T>(object x,string name)=>(T)x.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(x);
    static object Call(object x,string name,params object[] args)=>x.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(x,args);
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Finish(Exception e)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Directory.CreateDirectory(Output);
        if(e!=null){File.WriteAllText(Output+"/failure.txt",e.ToString());Debug.LogException(e);}
        else if(File.Exists(Output+"/failure.txt"))File.Delete(Output+"/failure.txt");
        if(Application.isBatchMode)EditorApplication.Exit(e==null?0:1);else EditorApplication.ExitPlaymode();
    }
}
