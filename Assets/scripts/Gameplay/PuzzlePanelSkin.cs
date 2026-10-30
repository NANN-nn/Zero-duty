using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Industrial controls made entirely from live UI geometry and text.</summary>
public sealed class PuzzlePanelSkin : MonoBehaviour
{
    private const float W=1672f,H=941f;
    private RectTransform panel, dimmer, shuttle, stability;
    private RectTransform[] handles;
    private NativePanelGraphic[] powerLamps, gasLamps;
    private NativePanelGraphic buttonFace;
    private readonly bool[] pulled=new bool[3];
    private readonly float[] wrong=new float[3];
    private int gasProgress;
    private bool held;
    private float stable;
    private readonly Color[] colors={new Color(.9f,.58f,.21f),new Color(.3f,.77f,.8f),new Color(.66f,.43f,.84f)};
    public Button GasButton {get;private set;}
    public Button[] PowerButtons {get;private set;}
    public event System.Action<bool> GasHoldChanged;

    private void Begin(CanvasGroup group)
    {
        foreach(Graphic g in group.GetComponentsInChildren<Graphic>(true)) g.enabled=false;
        panel=group.transform as RectTransform;
        panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);
        panel.anchoredPosition=Vector2.zero;panel.sizeDelta=new Vector2(W,H);
        dimmer=Rect("Scene shade",panel,835,470,2600,1600);
        var shade=dimmer.gameObject.AddComponent<Image>();shade.color=new Color(0,0,0,.58f);shade.raycastTarget=true;
        Plate("Outer chassis",panel,835,470,1532,825,new Color(.025f,.035f,.043f));
        Plate("Inset face",panel,835,470,1488,781,new Color(.065f,.078f,.086f));
        foreach(float x in new[]{115f,1555f}) foreach(float y in new[]{105f,835f})
        {
            Plate("Recessed fastener",panel,x,y,22,22,new Color(.1f,.12f,.13f),true);
            Bar("Fastener slot",panel,x,y,11,2,new Color(.015f,.02f,.025f));
        }
        Bar("Upper brushed edge",panel,835,119,1340,2,new Color(.3f,.34f,.35f));
        Bar("Lower brushed edge",panel,835,814,1340,2,new Color(.16f,.2f,.22f));
    }
    public void ConfigurePower(CanvasGroup group,Button[] oldButtons)
    {
        Begin(group);
        if(oldButtons!=null) foreach(Button b in oldButtons) if(b!=null)b.interactable=false;
        // Rotary contacts and their buttons are added by the existing routing game.
        Plate("Rotary circuit recess",panel,835,294,1140,245,new Color(.025f,.038f,.044f));
        Bar("Contact bus",panel,835,294,1010,7,new Color(.31f,.39f,.42f));
        for(int i=0;i<2;i++) Plate("Socket endpoint",panel,320+i*1030,294,24,24,new Color(.47f,.6f,.62f),true);
        Text("Replay icon","<",panel,1430,161,65,65,36);
        handles=new RectTransform[3];powerLamps=new NativePanelGraphic[3];PowerButtons=new Button[3];
        for(int i=0;i<3;i++)
        {
            float x=460+i*375;
            Plate("Breaker housing",panel,x,650,300,365,new Color(.035f,.047f,.055f));
            Plate("Lamp bezel",panel,x,535,70,70,new Color(.025f,.03f,.035f),true);
            powerLamps[i]=Plate("Breaker lamp",panel,x,535,48,48,Tint(colors[i],.24f),true);
            Plate("Lever track",panel,x,695,44,170,new Color(.012f,.017f,.02f));
            for(int t=0;t<5;t++) Bar("Lever scale",panel,x+48,630+t*30,12,2,new Color(.3f,.34f,.35f));
            handles[i]=Rect("Moving lever",panel,x,745,155,64);
            Plate("Handle shadow",handles[i],835,475,153,62,new Color(.014f,.02f,.024f));
            Plate("Grip",handles[i],835,466,145,54,new Color(.29f,.33f,.35f));
            for(int t=-2;t<=2;t++) Bar("Grip groove",handles[i],835+t*20,466,2,28,new Color(.15f,.18f,.2f));
            RectTransform area=Rect("Lever hit area "+i,panel,x,663,280,340);
            Image hit=area.gameObject.AddComponent<Image>();hit.color=Color.clear;
            PowerButtons[i]=area.gameObject.AddComponent<Button>();PowerButtons[i].targetGraphic=hit;
            PowerButtons[i].transition=Selectable.Transition.None;PowerButtons[i].navigation=new Navigation{mode=Navigation.Mode.None};
        }
    }
    public void ConfigureGas(CanvasGroup group,Slider oldSlider)
    {
        Begin(group);if(oldSlider!=null)oldSlider.gameObject.SetActive(false);
        gasLamps=new NativePanelGraphic[3];
        for(int i=0;i<3;i++)
        {
            float x=555+i*280;
            Plate("Channel socket",panel,x,258,124,124,new Color(.025f,.033f,.038f),true);
            gasLamps[i]=Plate("Channel lamp",panel,x,258,92,92,new Color(.055f,.072f,.077f),true);
            Text("Channel number","0"+(i+1),panel,x,161,90,38,24);
        }
        Plate("Pressure gauge recess",panel,835,452,1130,155,new Color(.018f,.027f,.03f));
        Bar("Safe pressure band",panel,835,452,990*.28f,123,new Color(.11f,.24f,.17f));
        for(int i=0;i<=50;i++) Bar("Gauge tick",panel,340+i*19.8f,452,2,i%5==0?62:24,new Color(.49f,.55f,.55f));
        shuttle=Rect("Moving pressure shuttle",panel,340,452,18,122);
        Plate("Needle grip",shuttle,835,470,16,120,new Color(.62f,.68f,.68f));
        Bar("Needle center",shuttle,835,470,3,112,new Color(.94f,.83f,.52f));
        Bar("Stability bed",panel,835,557,320,10,new Color(.018f,.027f,.03f));
        stability=Bar("Stability fill",panel,675,557,0,10,colors[0]).rectTransform;
        stability.pivot=new Vector2(0,.5f);
        Plate("Button recess",panel,835,674,310,190,new Color(.018f,.026f,.03f));
        buttonFace=Plate("Pressure button",panel,835,663,250,135,new Color(.27f,.31f,.33f));
        GasButton=buttonFace.gameObject.AddComponent<Button>();GasButton.targetGraphic=buttonFace;
        GasButton.transition=Selectable.Transition.None;GasButton.navigation=new Navigation{mode=Navigation.Mode.None};
        buttonFace.raycastTarget=true;
        Text("Hold label","HOLD",buttonFace.transform,835,470,215,80,32);
        EventTrigger events=buttonFace.gameObject.AddComponent<EventTrigger>();
        AddEvent(events,EventTriggerType.PointerDown,d=>{if(d is PointerEventData e && e.button==PointerEventData.InputButton.Left && GasButton.IsInteractable())SetHeld(true);});
        AddEvent(events,EventTriggerType.PointerUp,_=>ReleaseGasButton());
        AddEvent(events,EventTriggerType.PointerExit,_=>ReleaseGasButton());
    }
    private void SetHeld(bool value){if(held==value)return;held=value;GasHoldChanged?.Invoke(value);}
    public void ReleaseGasButton()=>SetHeld(false);
    public void SetGasStability(float seconds,float required,bool safe){stable=Mathf.Clamp01(seconds/Mathf.Max(.1f,required));if(stability!=null)stability.sizeDelta=new Vector2(320*stable,10);}
    public void ResetPowerLevers(){for(int i=0;i<3;i++){pulled[i]=false;wrong[i]=0;}}
    public void SetPowerLeverState(int index,bool value){if(index>=0&&index<3)pulled[index]=value;}
    public void PulseWrongLever(int index){if(index>=0&&index<3)wrong[index]=1;}
    public void SetGasProgress(int count)=>gasProgress=Mathf.Clamp(count,0,3);
    public void SetGasNeedlePosition(float value){if(shuttle!=null)shuttle.anchoredPosition=new Vector2(Mathf.Lerp(340,1330,Mathf.Clamp01(value))-W/2,H/2-452);}
    public void PulseGasButton(){}
    private void LateUpdate()
    {
        if(panel==null)return;
        Canvas canvas=panel.GetComponentInParent<Canvas>();
        RectTransform view=canvas!=null?canvas.transform as RectTransform:null;
        Vector2 size=view!=null?view.rect.size:new Vector2(Screen.width,Screen.height);
        float scale=Mathf.Min(size.x*.87f/W,size.y*.87f/H);panel.localScale=Vector3.one*scale;
        dimmer.sizeDelta=size/Mathf.Max(.01f,scale);
        if(handles!=null)for(int i=0;i<3;i++)
        {
            wrong[i]=Mathf.MoveTowards(wrong[i],0,Time.unscaledDeltaTime*3);
            Vector2 pos=handles[i].anchoredPosition;pos.y=Mathf.Lerp(pos.y,H/2-(pulled[i]?650:745),1-Mathf.Exp(-Time.unscaledDeltaTime*16));handles[i].anchoredPosition=pos;
            powerLamps[i].color=wrong[i]>.01f?new Color(.7f,.12f,.08f):Tint(colors[i],pulled[i]?1f:.24f);powerLamps[i].SetVerticesDirty();
        }
        if(gasLamps!=null)for(int i=0;i<3;i++){gasLamps[i].color=i<gasProgress?new Color(.35f,.75f,.53f):i==gasProgress?colors[0]*.65f:new Color(.055f,.072f,.077f);gasLamps[i].SetVerticesDirty();}
        if(buttonFace!=null){buttonFace.color=held?new Color(.15f,.2f,.22f):new Color(.27f,.31f,.33f);buttonFace.rectTransform.anchoredPosition=new Vector2(0,H/2-(held?669:663));buttonFace.SetVerticesDirty();}
    }
    private void OnDisable()=>ReleaseGasButton();
    private static Color Tint(Color c,float level)=>new Color(c.r*level,c.g*level,c.b*level,1f);
    private static void AddEvent(EventTrigger t,EventTriggerType type,UnityEngine.Events.UnityAction<BaseEventData> callback){var e=new EventTrigger.Entry{eventID=type};e.callback.AddListener(callback);t.triggers.Add(e);}
    private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x-W/2,H/2-y);r.sizeDelta=new Vector2(w,h);return r;}
    private static NativePanelGraphic Plate(string name,Transform p,float x,float y,float w,float h,Color c,bool round=false){var g=Rect(name,p,x,y,w,h).gameObject.AddComponent<NativePanelGraphic>();g.color=c;g.round=round;g.raycastTarget=false;return g;}
    private static Image Bar(string name,Transform p,float x,float y,float w,float h,Color c){var g=Rect(name,p,x,y,w,h).gameObject.AddComponent<Image>();g.color=c;g.raycastTarget=false;return g;}
    private static void Text(string name,string content,Transform p,float x,float y,float w,float h,float size){var t=Rect(name,p,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=StationTypography.UIAsset;StationLocalization.Set(t,content);t.fontSize=size;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.color=new Color(.78f,.83f,.83f);}
}
