using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Mesh-only industrial UI parts shared by the remaining rebuilt screens.</summary>
public static class NativeInterfaceParts
{
    public static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;
        rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    public static NativePanelGraphic Plate(string name,Transform parent,Vector2 position,Vector2 size,Color color,Color border)
    {
        var plate=Rect(name,parent,position,size).gameObject.AddComponent<NativePanelGraphic>();
        plate.color=color;plate.border=border;plate.raycastTarget=false;return plate;
    }
    public static Image Bar(string name,Transform parent,Vector2 position,Vector2 size,Color color)
    {
        var image=Rect(name,parent,position,size).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;
    }
    public static void Chassis(Transform parent,Vector2 size)
    {
        Color edge=new Color(.23f,.31f,.35f);
        Plate("Inset face",parent,Vector2.zero,size-new Vector2(26,26),new Color(.045f,.065f,.079f),new Color(.105f,.15f,.17f));
        Bar("Header seam",parent,new Vector2(0,size.y*.5f-48),new Vector2(size.x-90,2),edge);
        Bar("Lower seam",parent,new Vector2(0,-size.y*.5f+40),new Vector2(size.x-90,2),new Color(.12f,.19f,.22f));
        foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})
        {
            Vector2 at=new Vector2(x*(size.x*.5f-23),y*(size.y*.5f-23));
            var screw=Plate("Recessed screw",parent,at,Vector2.one*12,new Color(.09f,.12f,.13f),edge);screw.round=true;
            Bar("Screw slot",screw.transform,Vector2.zero,new Vector2(6,1.5f),new Color(.018f,.025f,.028f));
        }
        for(int i=0;i<5;i++)Bar("Lower vent",parent,new Vector2(size.x*.5f-125+i*12,-size.y*.5f+26),new Vector2(6,2),edge);
    }
    public static NativePanelGraphic ButtonFace(Transform parent,Vector2 size,Color accent)
    {
        var face=Plate("Button face",parent,Vector2.zero,size,new Color(.075f,.11f,.13f),new Color(.24f,.35f,.39f));
        Bar("Upper bevel",face.transform,new Vector2(0,size.y*.5f-7),new Vector2(size.x-30,2),new Color(.16f,.23f,.26f));
        var lamp=Bar("Hover indicator",face.transform,new Vector2(-size.x*.5f+14,0),new Vector2(3,size.y-24),accent*.28f);
        lamp.color=new Color(accent.r*.28f,accent.g*.28f,accent.b*.28f,1);
        var feedback=parent.gameObject.AddComponent<NativeButtonFeedback>();feedback.Configure(lamp,accent);
        return face;
    }
}

public sealed class NativeButtonFeedback:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
{
    private Image lamp;private Color accent;private bool hovered;
    public void Configure(Image image,Color color){lamp=image;accent=color;Refresh(false);}
    private void Refresh(bool pressed)
    {
        if(lamp==null)return;float value=pressed?1.5f:hovered?1:.28f;
        lamp.color=new Color(accent.r*value,accent.g*value,accent.b*value,1);
    }
    public void OnPointerEnter(PointerEventData e){hovered=true;Refresh(false);}
    public void OnPointerExit(PointerEventData e){hovered=false;Refresh(false);}
    public void OnPointerDown(PointerEventData e){Refresh(true);}
    public void OnPointerUp(PointerEventData e){Refresh(false);}
    private void OnDisable(){hovered=false;Refresh(false);}
}
