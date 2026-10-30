using UnityEngine;
using UnityEngine.UI;

/// <summary>Unruled paper and an open book drawn entirely as UI vertex geometry.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NativePaperGraphic:MaskableGraphic
{
    public NativePaperGraphic(){useLegacyMeshGeneration=false;}
    private OptionalPropInteraction.NotePaperStyle style;
    public void SetStyle(OptionalPropInteraction.NotePaperStyle value){style=value;SetVerticesDirty();}
    private Vector2 scale;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if(style==OptionalPropInteraction.NotePaperStyle.Book)
        {
            scale=new Vector2(rectTransform.rect.width/1600,rectTransform.rect.height/900);
            Quad(vh,-710,-425,710,412,new Color(0,0,0,.3f));
            Quad(vh,-696,-415,696,415,new Color(.17f,.21f,.20f));
            Quad(vh,-679,-405,679,407,new Color(.39f,.40f,.35f));
            for(int i=0;i<4;i++)Quad(vh,-666+i,-398+i*3,666-i,-395+i*3,new Color(.54f+i*.035f,.54f+i*.035f,.49f+i*.035f));
            Quad(vh,-660,-384,-12,403,new Color(.76f,.75f,.67f),new Color(.90f,.88f,.78f));
            Quad(vh,12,-384,660,403,new Color(.77f,.76f,.68f),new Color(.92f,.90f,.80f));
            for(int i=0;i<18;i++)
            {
                float a=(1-i/18f)*.12f;
                Quad(vh,-30-i*2,-384,-28-i*2,403,new Color(.16f,.14f,.1f,a));
                Quad(vh,28+i*2,-384,30+i*2,403,new Color(.16f,.14f,.1f,a));
            }
            Quad(vh,-12,-390,12,405,new Color(.27f,.28f,.24f));
            Quad(vh,-3,-390,3,405,new Color(.11f,.13f,.12f,.7f));
            for(int i=0;i<6;i++)Quad(vh,-5,-310+i*123,5,-297+i*123,new Color(.49f,.48f,.41f));
            Quad(vh,605,-418,624,-350,new Color(.26f,.34f,.35f));
        }
        else if(style==OptionalPropInteraction.NotePaperStyle.Sticky)
        {
            scale=new Vector2(rectTransform.rect.width/940,rectTransform.rect.height/856);
            Quad(vh,-372,-370,388,350,new Color(0,0,0,.24f));
            Quad(vh,-380,-352,380,360,new Color(.70f,.65f,.41f),new Color(.90f,.85f,.59f));
            Quad(vh,-380,307,380,360,new Color(.45f,.40f,.24f,.12f));
            Triangle(vh,new Vector2(325,-352),new Vector2(380,-297),new Vector2(325,-297),new Color(.96f,.91f,.68f));
            Triangle(vh,new Vector2(325,-352),new Vector2(380,-352),new Vector2(380,-297),new Color(.49f,.44f,.29f));
        }
        else
        {
            scale=new Vector2(rectTransform.rect.width/720,rectTransform.rect.height/850);
            Quad(vh,-347,-409,357,398,new Color(0,0,0,.28f));
            Quad(vh,-350,-400,350,405,new Color(.78f,.79f,.75f),new Color(.93f,.94f,.89f));
            Quad(vh,-347,-397,347,-395,new Color(.62f,.65f,.61f));
            // Small folded corner and a clip; the writing area stays completely unruled.
            Triangle(vh,new Vector2(312,405),new Vector2(350,367),new Vector2(312,367),new Color(.97f,.97f,.92f));
            Quad(vh,-38,383,38,425,new Color(.20f,.24f,.25f));
            Quad(vh,-31,389,31,422,new Color(.48f,.53f,.53f),new Color(.69f,.72f,.69f));
            Quad(vh,-23,397,23,419,new Color(.24f,.28f,.28f));
        }
    }
    private Vector2 Point(float x,float y)=>Vector2.Scale(new Vector2(x,y),scale)+rectTransform.rect.center;
    private void Quad(VertexHelper vh,float x1,float y1,float x2,float y2,Color bottom,Color? top=null)
    {
        int n=vh.currentVertCount;Color upper=top??bottom;
        vh.AddVert(Point(x1,y1),bottom,Vector2.zero);vh.AddVert(Point(x1,y2),upper,Vector2.zero);
        vh.AddVert(Point(x2,y2),upper,Vector2.zero);vh.AddVert(Point(x2,y1),bottom,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
    }
    private void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color tint)
    {
        int n=vh.currentVertCount;vh.AddVert(Point(a.x,a.y),tint,Vector2.zero);vh.AddVert(Point(b.x,b.y),tint,Vector2.zero);vh.AddVert(Point(c.x,c.y),tint,Vector2.zero);vh.AddTriangle(n,n+1,n+2);
    }
}
