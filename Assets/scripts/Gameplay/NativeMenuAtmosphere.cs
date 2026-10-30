using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Menu-only live-screen interference and a practical warning light.</summary>
public sealed class NativeMenuAtmosphere : MonoBehaviour
{
    private readonly List<GameObject> overlays=new List<GameObject>();
    private readonly List<Material> materials=new List<Material>();
    private readonly float[] nextBurst=new float[3],burstUntil=new float[3];
    private readonly System.Random random=new System.Random();
    private Light warning;
    private GameObject lens;
    private Material lensMaterial;
    private float nextWarning,warningStarted=-100f;
    private bool released;
    public Bounds DeskBounds {get;private set;}

    public static void FrameIntroduction(Camera camera, MonitorScreenClick screens)
    {
        bool found=false;Bounds bounds=new Bounds();
        for(int i=0;i<3;i++)
        {
            Renderer screen=screens!=null?screens.GetDisplay(i):null;
            if(screen==null)continue;
            if(!found){bounds=screen.bounds;found=true;}else bounds.Encapsulate(screen.bounds);
        }
        if(!found)return;
        camera.fieldOfView=48;camera.aspect=16f/9;
        float tanH=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)*camera.aspect;
        float distance=Mathf.Max(2.2f,bounds.size.z/(2*tanH*.82f));
        Vector3 center=bounds.center-Vector3.up*.24f;
        camera.transform.SetPositionAndRotation(center-Vector3.right*distance,Quaternion.LookRotation(Vector3.right,Vector3.up));
        camera.ResetProjectionMatrix();
    }

    public void Initialize(Camera camera,MonitorCameraController feeds)
    {
        MonitorScreenClick screens=FindFirstObjectByType<MonitorScreenClick>();
        bool found=false;Bounds bounds=new Bounds();
        for(int i=0;i<3;i++)
        {
            Renderer screen=screens!=null?screens.GetDisplay(i):null;
            if(screen==null)continue;
            if(!found){bounds=screen.bounds;found=true;}else bounds.Encapsulate(screen.bounds);
        }
        if(!found)return;
        Vector3 screenCenter=bounds.center;
        foreach(Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if(!r.name.StartsWith("Console module |")&&!r.name.StartsWith("Console |"))continue;
            Vector3 offset=r.bounds.center-screenCenter;offset.y=0;
            if(offset.magnitude<3.1f)bounds.Encapsulate(r.bounds);
        }
        // Include the desktop/base, not just the three glass screen rectangles.
        bounds.Encapsulate(new Vector3(bounds.center.x,.05f,bounds.center.z));
        DeskBounds=bounds;
        Quaternion rotation=Quaternion.LookRotation(new Vector3(1,-.09f,0),Vector3.up);
        camera.fieldOfView=64;camera.aspect=16f/9f;
        float tanV=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f),tanH=tanV*camera.aspect;
        float distance=0;
        for(int i=0;i<8;i++)
        {
            Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            Vector3 local=Quaternion.Inverse(rotation)*(corner-bounds.center);
            distance=Mathf.Max(distance,Mathf.Abs(local.x)/(.53f*tanH)-local.z,Mathf.Abs(local.y)/(.74f*tanV)-local.z);
        }
        // Closer, slightly elevated framing: keep the monitors readable while
        // the foreground chair's lower half falls below the frame.
        camera.transform.SetPositionAndRotation(bounds.center-rotation*Vector3.forward*((distance+.25f)*.7f)+Vector3.up*.65f,rotation*Quaternion.Euler(4f,0,0));
        camera.ResetProjectionMatrix();Matrix4x4 projection=camera.projectionMatrix;projection.m02=-.34f;camera.projectionMatrix=projection;

        Shader shader=Resources.Load<Shader>("ZeroHourAssets/Shaders/NativeMenuScreen");
        for(int i=0;i<3;i++)
        {
            Renderer screen=screens.GetDisplay(i);MeshFilter filter=screen!=null?screen.GetComponent<MeshFilter>():null;
            Camera feed=feeds!=null?feeds.GetCamera(i):null;
            if(shader==null||filter==null||feed==null||feed.targetTexture==null)continue;
            Bounds rect=filter.sharedMesh.bounds;
            float side=Mathf.Sign(screen.transform.InverseTransformPoint(camera.transform.position).y);if(side==0)side=1;
            GameObject overlay=GameObject.CreatePrimitive(PrimitiveType.Quad);overlay.name="Menu screen interference "+i;
            Collider collider=overlay.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            overlay.transform.SetParent(screen.transform,false);
            overlay.transform.localPosition=rect.center+Vector3.up*side*(rect.extents.y+.03f);
            overlay.transform.localRotation=Quaternion.Euler(90,0,0);
            overlay.transform.localScale=new Vector3(rect.size.x*.98f,rect.size.z*.98f,1);
            Material material=new Material(shader){name="Native menu screen "+i};material.SetTexture("_BaseMap",feed.targetTexture);material.SetFloat("_Brightness",1);
            Renderer renderer=overlay.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            overlays.Add(overlay);materials.Add(material);nextBurst[i]=Time.unscaledTime+1f+i*.7f;
        }
        GameObject lamp=new GameObject("Menu red warning light");lamp.transform.SetParent(transform,false);
        lamp.transform.position=bounds.center+Vector3.up*(bounds.extents.y+.25f)-Vector3.right*.5f+Vector3.forward*bounds.extents.z*.65f;
        warning=lamp.AddComponent<Light>();warning.type=LightType.Point;warning.color=new Color(1,.025f,.012f);warning.range=7;warning.shadows=LightShadows.None;
        lens=GameObject.CreatePrimitive(PrimitiveType.Sphere);lens.name="Menu warning lens";lens.transform.SetParent(lamp.transform,false);lens.transform.localScale=Vector3.one*.09f;
        Collider lensCollider=lens.GetComponent<Collider>();lensCollider.enabled=false;Destroy(lensCollider);
        lensMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));lens.GetComponent<Renderer>().sharedMaterial=lensMaterial;
        nextWarning=Time.unscaledTime+2.8f;SetWarningLamp(0);
    }
    public void Refresh(float now)
    {
        if(released)return;
        for(int i=0;i<materials.Count;i++)
        {
            if(now>=nextBurst[i]){burstUntil[i]=now+.3f+(float)random.NextDouble()*.25f;nextBurst[i]=now+2f+(float)random.NextDouble()*4.5f;}
            bool burst=now<burstUntil[i];float beat=Mathf.Repeat(now*13f+i*.2f,1f);
            materials[i].SetFloat("_FaultTime",now+i*3.7f);
            materials[i].SetFloat("_CueAmount",burst?1:0);
            materials[i].SetFloat("_Brightness",burst?(beat<.42f?.08f:1.25f):.88f);
        }
        if(now>=nextWarning){warningStarted=now;nextWarning=now+3.2f+(float)random.NextDouble()*3f;}
        float t=now-warningStarted;
        SetWarningLamp(t>=0&&(t<.12f||t>.23f&&t<.43f)?1:0);
    }
    private void SetWarningLamp(float amount)
    {
        if(warning==null)return;warning.intensity=amount*3.8f;warning.enabled=amount>0;
        if(lensMaterial!=null)lensMaterial.color=Color.Lerp(new Color(.12f,.015f,.01f),new Color(2.5f,.025f,.01f),amount);
    }
    public void Release()
    {
        if(released)return;released=true;
        foreach(GameObject overlay in overlays)if(overlay!=null){overlay.SetActive(false);Destroy(overlay);}
        foreach(Material material in materials)if(material!=null)Destroy(material);
        if(warning!=null)warning.gameObject.SetActive(false);
        if(lensMaterial!=null)Destroy(lensMaterial);
    }
    private void OnDestroy()=>Release();
}
