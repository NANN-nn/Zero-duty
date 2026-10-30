using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum NativeEndingKind { DataTransfer, Lockdown, Failure }

/// <summary>Short, deterministic real-time shots. Only visual meshes are copied;
/// the live level, doors, puzzle state and lights are never animated by this film.</summary>
public sealed class NativeEndingFilm : MonoBehaviour
{
    private const int FilmLayer = 30;
    private static readonly Vector3 SourceOrigin = new Vector3(1.419f, .342f, -11.6f);
    private readonly List<Material> ownedMaterials = new List<Material>();
    private Transform stage, leftDoor, rightDoor, leftClaw, rightClaw;
    private Camera filmCamera;
    private NativeShadowActor shadow;
    private Light coldLight, warningLight, interiorLight;
    private Material statusMaterial;
    private Material gradeMaterial;
    private readonly Material[] departureLamps = new Material[3];
    private RenderTexture cameraTexture;
    private AudioSource mechanicalSource;
    private AudioClip mechanicalClip;
    private NativeEndingKind kind;
    private float started;
    public RenderTexture Texture { get; private set; }
    public bool Finished { get; private set; }
    public float Duration => kind == NativeEndingKind.Failure ? 4.2f : 6.5f;
    public int CopiedMeshCount { get; private set; }
    public float DoorOpening { get; private set; }
    public float LiftDeparture { get; private set; }

    public void Play(NativeEndingKind result)
    {
        kind = result;
        stage = new GameObject("Ending film stage - isolated real corridor").transform;
        stage.SetParent(transform, false); stage.position = new Vector3(-1000, 100, 0);
        CopyCorridor();
        Material metal = Lit(new Color(.12f, .16f, .19f), .62f, .45f);
        Material inset = Lit(new Color(.034f, .052f, .063f), .5f, .36f);
        Material rail = Lit(new Color(.28f, .35f, .38f), .8f, .67f);
        Material glow = Unlit(new Color(.28f, .65f, .8f) * 1.2f);
        statusMaterial = Unlit(new Color(.7f, .04f, .025f));
        BuildSlidingDoor(metal, inset, rail, glow);
        // A small lift cabin behind the sliding leaves, built as ordinary meshes.
        Box("Cabin floor", new Vector3(0, -.05f, -1.7f), new Vector3(3.2f, .1f, 4.2f), metal);
        Box("Cabin rear", new Vector3(0, 1.55f, -3.7f), new Vector3(3.2f, 3.2f, .16f), inset);
        for (int side=-1;side<=1;side+=2)
        {
            Box("Cabin wall", new Vector3(side*1.56f,1.55f,-1.7f),new Vector3(.12f,3.2f,4.2f),metal);
            Box("Cabin hand rail",new Vector3(side*1.47f,.9f,-1.7f),new Vector3(.05f,.06f,3.8f),rail);
            Box("Cabin vertical light",new Vector3(side*1.35f,1.6f,-3.59f),new Vector3(.035f,2.7f,.035f),glow);
        }
        Box("Cabin ceiling",new Vector3(0,3.12f,-1.7f),new Vector3(3.2f,.12f,4.2f),metal);
        filmCamera = new GameObject("Ending film camera").AddComponent<Camera>();
        filmCamera.transform.SetParent(stage, false); filmCamera.enabled = false;
        filmCamera.clearFlags = CameraClearFlags.SolidColor; filmCamera.backgroundColor = Color.black;
        filmCamera.cullingMask = 1 << FilmLayer; filmCamera.nearClipPlane = .025f; filmCamera.farClipPlane = 35;
        filmCamera.fieldOfView = 53; filmCamera.aspect = 16f / 9;
        filmCamera.allowHDR = true; filmCamera.allowMSAA = false;
        filmCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        Texture = new RenderTexture(1344, 756, 24, RenderTextureFormat.ARGB32) { name = "Native ending live picture" };
        Texture.Create();
        cameraTexture = new RenderTexture(1344,756,24,RenderTextureFormat.ARGB32) { name="Ending camera raw" };
        cameraTexture.Create();filmCamera.targetTexture=cameraTexture;
        gradeMaterial=new Material(Resources.Load<Shader>("ZeroHourAssets/Shaders/NativeFilmGrade"));
        ownedMaterials.Add(gradeMaterial);
        coldLight = Lamp("Hallway soft light", new Vector3(0,2.65f,4), new Color(.53f,.7f,.8f), 3.6f, 11);
        interiorLight = Lamp("Lift backlight", new Vector3(0,2.5f,-2.8f), new Color(.48f,.72f,.84f), 5, 6);
        warningLight = Lamp("Door warning",new Vector3(.9f,2.7f,1),new Color(1,.025f,.008f),3,5);
        shadow = NativeShadowActor.Create(stage, FilmLayer);
        leftClaw = shadow.CreateForegroundClaw(filmCamera.transform, FilmLayer, -1);
        rightClaw = shadow.CreateForegroundClaw(filmCamera.transform, FilmLayer, 1);
        leftClaw.gameObject.SetActive(false); rightClaw.gameObject.SetActive(false);
        CreateMechanicalAudio();
        started = Time.unscaledTime;
        Sample(0);
    }

    private void CopyCorridor()
    {
        foreach (MeshRenderer source in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!source.enabled || source.gameObject.layer == FilmLayer || !source.name.StartsWith("Corridor")) continue;
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var clone = new GameObject(source.name, typeof(MeshFilter), typeof(MeshRenderer));
            clone.layer = FilmLayer; clone.transform.SetParent(stage, false);
            clone.transform.localPosition = source.transform.position - SourceOrigin;
            clone.transform.localRotation = source.transform.rotation;
            clone.transform.localScale = source.transform.lossyScale;
            clone.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = clone.GetComponent<MeshRenderer>(); renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            CopiedMeshCount++;
        }
    }

    private void BuildSlidingDoor(Material metal, Material inset, Material rail, Material glow)
    {
        for (int side=-1;side<=1;side+=2)
        {
            Box("Lift jamb",new Vector3(side*1.35f,1.45f,.12f),new Vector3(.3f,2.9f,.4f),metal);
            Box("Jamb recess",new Vector3(side*1.29f,1.45f,.34f),new Vector3(.09f,2.65f,.03f),inset);
            Box("Jamb light",new Vector3(side*1.21f,1.45f,.36f),new Vector3(.028f,2.65f,.025f),glow);
            var leaf = new GameObject(side<0?"Lift leaf L":"Lift leaf R").transform;
            leaf.SetParent(stage,false); leaf.localPosition = new Vector3(side*.6f,0,.10f);
            Box("Sliding leaf",new Vector3(0,1.4f,0),new Vector3(1.19f,2.8f,.13f),metal,leaf);
            Box("Recessed leaf panel",new Vector3(0,1.48f,.077f),new Vector3(1.02f,2.28f,.025f),inset,leaf);
            Box("Central leaf insert",new Vector3(0,1.45f,.094f),new Vector3(.91f,2.06f,.018f),metal,leaf);
            Box("Vertical inner seam",new Vector3(-side*.54f,1.4f,.088f),new Vector3(.025f,2.65f,.035f),rail,leaf);
            for(int i=0;i<4;i++)Box("Lower leaf vent",new Vector3(0,.26f+i*.045f,.11f),new Vector3(.71f,.013f,.02f),inset,leaf);
            if(side<0)leftDoor=leaf;else rightDoor=leaf;
        }
        Box("Lift lintel",new Vector3(0,2.93f,.1f),new Vector3(3,.26f,.44f),metal);
        Box("Threshold",new Vector3(0,.017f,.17f),new Vector3(2.8f,.034f,.65f),rail);
        for(int i=-2;i<=2;i++)Box("Threshold runner",new Vector3(0,.038f,.14f+i*.075f),new Vector3(2.7f,.012f,.018f),inset);
        Box("Lift status strip",new Vector3(0,2.93f,.333f),new Vector3(.72f,.045f,.025f),statusMaterial);
        for(int i=0;i<departureLamps.Length;i++)
        {
            departureLamps[i]=Unlit(new Color(.035f,.065f,.075f));
            for(int side=-1;side<=1;side+=2)
            {
                Transform stroke=Box("Lift upward indicator",new Vector3(.98f+side*.027f,2.855f+i*.07f,.34f),new Vector3(.076f,.016f,.018f),departureLamps[i]);
                stroke.localRotation=Quaternion.Euler(0,0,-side*35);
            }
        }
        Box("Access reader",new Vector3(1.43f,1.2f,.4f),new Vector3(.17f,.31f,.1f),inset);
        Box("Access light",new Vector3(1.43f,1.23f,.458f),new Vector3(.09f,.035f,.018f),statusMaterial);
    }

    private void LateUpdate()
    {
        if (Texture == null || Finished) return;
        float elapsed = Time.unscaledTime - started;
        Sample(elapsed);
        if (elapsed >= Duration) { Finished = true; mechanicalSource.Stop(); }
    }

    /// <summary>Deterministic sampling also lets the editor inspect key frames.</summary>
    public void Sample(float elapsed)
    {
        float t = Mathf.Clamp(elapsed, 0, Duration);
        float opening;
        float grasp=0;
        float facing=0;
        LiftDeparture=0;
        Vector3 eye;
        Vector3 target;
        if (kind == NativeEndingKind.DataTransfer)
        {
            // The anomaly starts in the laboratory, enters the lift, then departs.
            opening = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1.45f,t))
                * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.25f,5.45f,t)));
            float advance = Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.55f,4.05f,t));
            LiftDeparture=Mathf.SmoothStep(0,1,Mathf.InverseLerp(5.5f,6.4f,t));
            shadow.transform.localPosition = new Vector3(.04f, .03f+LiftDeparture*.7f, Mathf.Lerp(2.45f,-1.95f,advance));
            facing=180;
            eye = new Vector3(-.12f,1.62f,Mathf.Lerp(6.1f,5.7f,t/Duration));
            target = new Vector3(0,1.48f,0);
            bool flicker = (t>1.4f&&t<1.54f)||(t>3.15f&&t<3.28f);
            coldLight.intensity = flicker ? .10f : 3.6f;
            warningLight.intensity = t<5.45f ? (Mathf.Sin(t*12)>0 ? 2.3f:.04f) : .15f;
            statusMaterial.SetColor("_BaseColor",new Color(.18f,.55f,.65f));
            shadow.Pose(.08f+.08f*Mathf.Sin(advance*Mathf.PI),t);
        }
        else if (kind == NativeEndingKind.Lockdown)
        {
            // The lift stays sealed behind the anomaly as it approaches the player in the lab.
            opening = 0;
            float approach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,5.6f,t));
            shadow.transform.localPosition = new Vector3(Mathf.Lerp(-.3f,.05f,approach), .025f, Mathf.Lerp(1.3f,4.1f,approach));
            eye = new Vector3(.35f,1.62f,6.4f);
            Vector3 towardPlayer=eye-shadow.transform.localPosition;
            facing=Mathf.Atan2(towardPlayer.x,towardPlayer.z)*Mathf.Rad2Deg;
            target = new Vector3(0,1.42f,.35f);
            coldLight.intensity = Mathf.Lerp(1.9f,4.3f,Mathf.InverseLerp(3.5f,5,t));
            warningLight.intensity = t<3.5f ? (Mathf.Sin(t*14)>0?3:.04f) : 0;
            statusMaterial.SetColor("_BaseColor", t<3.5f?new Color(.7f,.04f,.025f):new Color(.12f,.65f,.4f));
            shadow.Pose(Mathf.Lerp(.1f,.9f,approach),t);
        }
        else
        {
            opening=1;
            float approach = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6f,2.6f,t));
            eye = new Vector3(0,1.62f,7.0f);
            target = new Vector3(0,1.63f,0);
            shadow.transform.localPosition = new Vector3(0, -.55f, Mathf.Lerp(1.6f,5.5f,approach));
            coldLight.intensity = t>.75f&&t<1.05f ? .08f : Mathf.Lerp(2.6f,.65f,approach);
            warningLight.intensity = t<2.5f ? (Mathf.Sin(t*17)>0?2:.01f):.08f;
            shadow.Pose(approach,t);
            float grab = Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.45f,3.45f,t));
            grasp=grab;
            leftClaw.gameObject.SetActive(grab>0); rightClaw.gameObject.SetActive(grab>0);
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;Transform claw=i==0?leftClaw:rightClaw;
                claw.localPosition=new Vector3(side*Mathf.Lerp(.7f,.24f,grab),Mathf.Lerp(-.28f,.09f,grab),Mathf.Lerp(.85f,.42f,grab));
                claw.localScale=Vector3.one*1.15f;
                claw.localRotation=Quaternion.Euler(-10,0,side*70);
            }
            float shake = t>2.45f&&t<3.3f ? (1-Mathf.InverseLerp(2.45f,3.3f,t))*.045f : 0;
            eye+=new Vector3(Mathf.Sin(t*62)*shake,Mathf.Sin(t*47)*shake,0);
        }
        shadow.transform.localRotation=Quaternion.Euler(0,facing,0);
        DoorOpening=opening;
        for(int i=0;i<departureLamps.Length;i++)
        {
            bool rising=LiftDeparture>0&&i==Mathf.FloorToInt(Mathf.Repeat((t-5.5f)*5,3));
            departureLamps[i].SetColor("_BaseColor",rising?new Color(.38f,.85f,1):new Color(.035f,.065f,.075f));
        }
        leftDoor.localPosition=new Vector3(-.6f-opening*1.18f,0,.10f);
        rightDoor.localPosition=new Vector3(.6f+opening*1.18f,0,.10f);
        filmCamera.transform.localPosition=eye;
        filmCamera.transform.localRotation=Quaternion.LookRotation(target-eye,Vector3.up);
        RenderTexture previous=RenderTexture.active;
        try
        {
            filmCamera.Render();
            gradeMaterial.SetFloat("_Grab",grasp);gradeMaterial.SetFloat("_FilmTime",t);
            Graphics.Blit(cameraTexture,Texture,gradeMaterial);
        }
        finally{RenderTexture.active=previous;}
    }

    private Transform Box(string name,Vector3 position,Vector3 size,Material material,Transform parent=null)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.layer=FilmLayer;
        go.transform.SetParent(parent!=null?parent:stage,false);go.transform.localPosition=position;go.transform.localScale=size;
        Collider collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        go.GetComponent<Renderer>().sharedMaterial=material;
        return go.transform;
    }

    private Light Lamp(string name,Vector3 position,Color color,float intensity,float range)
    {
        var lamp=new GameObject(name).AddComponent<Light>();lamp.transform.SetParent(stage,false);lamp.transform.localPosition=position;
        lamp.type=LightType.Point;lamp.color=color;lamp.intensity=intensity;lamp.range=range;lamp.cullingMask=1<<FilmLayer;lamp.shadows=LightShadows.None;
        return lamp;
    }

    private Material Lit(Color color,float metallic,float smoothness)
    {
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",color);
        material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smoothness);ownedMaterials.Add(material);return material;
    }

    private Material Unlit(Color color)
    {
        var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",color);ownedMaterials.Add(material);return material;
    }

    private void CreateMechanicalAudio()
    {
        // Short synthesized motor / relay / air-release cues, with no voice or imported art.
        const int rate=22050;int count=Mathf.CeilToInt(Duration*rate);var samples=new float[count];var random=new System.Random(712);
        float low=0;
        for(int i=0;i<count;i++)
        {
            float t=i/(float)rate;float noise=(float)random.NextDouble()*2-1;low=Mathf.Lerp(low,noise,.12f);
            float value=0;
            if(kind==NativeEndingKind.Failure)
            {
                float surge=Mathf.Clamp01((t-.65f)/1.9f)*(1-Mathf.Clamp01((t-2.6f)/.5f));
                value=low*surge*.18f;
                float impact=Mathf.Exp(-Mathf.Max(0,t-2.55f)*13)*(t>=2.55f?1:0);
                value+=impact*(low*.3f+Mathf.Sin(t*210)*.12f);
            }
            else if(kind==NativeEndingKind.Lockdown)
            {
                // Locked relays and contact against a sealed door; no opening motor.
                for(int knock=0;knock<3;knock++)
                {
                    float hit=knock==0?.45f:knock==1?2.65f:3.15f;
                    if(t>=hit)value+=Mathf.Exp(-(t-hit)*28)*(low*.15f+Mathf.Sin(t*340)*.04f);
                }
            }
            else
            {
                float moving=MotorEnvelope(t,.35f,1.45f)+MotorEnvelope(t,4.25f,5.45f);
                value=moving*(low*.12f+Mathf.Sin(t*2*Mathf.PI*92)*.055f);
                if(t>=5.45f)value+=Mathf.Exp(-(t-5.45f)*20)*(noise*.11f+Mathf.Sin(t*540)*.08f);
                value+=MotorEnvelope(t,5.5f,6.5f)*(low*.075f+Mathf.Sin(t*2*Mathf.PI*(70+t*4))*.025f);
            }
            samples[i]=value;
        }
        mechanicalClip=AudioClip.Create("Native ending mechanical cue",count,1,rate,false);mechanicalClip.SetData(samples,0);
        mechanicalSource=gameObject.AddComponent<AudioSource>();mechanicalSource.playOnAwake=false;mechanicalSource.spatialBlend=0;
        mechanicalSource.volume=.58f;mechanicalSource.clip=mechanicalClip;mechanicalSource.Play();
    }

    private static float MotorEnvelope(float t,float start,float end) =>
        Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,start+.2f,t))
        *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(end-.2f,end,t)));

    private void OnDestroy()
    {
        if(mechanicalSource!=null)mechanicalSource.Stop();
        if(mechanicalClip!=null)Destroy(mechanicalClip);
        if(filmCamera!=null)filmCamera.targetTexture=null;
        if(RenderTexture.active==cameraTexture||RenderTexture.active==Texture)RenderTexture.active=null;
        if(cameraTexture!=null){cameraTexture.Release();Destroy(cameraTexture);}
        if(Texture!=null){Texture.Release();Destroy(Texture);}
        foreach(Material material in ownedMaterials)if(material!=null)Destroy(material);
    }
}
