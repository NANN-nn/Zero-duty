using UnityEngine;
using UnityEngine.UI;

/// <summary>Editable UI geometry. No imported bitmap or generated image is used.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class NativePanelGraphic : MaskableGraphic
{
    public NativePanelGraphic(){useLegacyMeshGeneration=false;}
    public bool round;
    public Color border = new Color(.28f, .34f, .37f);
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        Draw(vh, r, border);
        r.xMin += 3; r.xMax -= 3; r.yMin += 3; r.yMax -= 3;
        Draw(vh, r, color);
    }
    private void Draw(VertexHelper vh, Rect r, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(r.center, tint, Vector2.zero);
        int count = round ? 48 : 8;
        float cut = Mathf.Min(16f, Mathf.Min(r.width,r.height)*.2f);
        Vector2[] corners = {
            new Vector2(r.xMin+cut,r.yMin), new Vector2(r.xMax-cut,r.yMin),
            new Vector2(r.xMax,r.yMin+cut), new Vector2(r.xMax,r.yMax-cut),
            new Vector2(r.xMax-cut,r.yMax), new Vector2(r.xMin+cut,r.yMax),
            new Vector2(r.xMin,r.yMax-cut), new Vector2(r.xMin,r.yMin+cut) };
        for (int i=0;i<count;i++)
        {
            float a=i*Mathf.PI*2/count;
            Vector2 v=round ? r.center+new Vector2(Mathf.Cos(a)*r.width/2,Mathf.Sin(a)*r.height/2) : corners[i];
            vh.AddVert(v,tint,Vector2.zero);
        }
        for(int i=0;i<count;i++) vh.AddTriangle(start,start+1+i,start+1+(i+1)%count);
    }
}
