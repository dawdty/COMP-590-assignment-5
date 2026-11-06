using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class LightsaberController : MonoBehaviour
{
    public GameObject blade;
    [Min(0.02f)] public float transitionSeconds = 0.25f;
    [Min(0f)] public float openingDelaySeconds = 0.2f;
    [Min(0f)] public float closingDelaySeconds = 0.8f;
    public bool startOn = true;
    public float Extension { get; private set; }
    public bool TargetOn { get; private set; }
    public Color BladeColor => colors[colorIndex];

    readonly Color[] colors = { new Color(1f, 0f, 0.8614f), new Color(0.02f, 0.5f, 1f),
        new Color(0.1f, 1f, 0.15f), new Color(1f, 0.02f, 0.01f), new Color(1f, 0.55f, 0.01f) };
    int colorIndex;
    Vector3 initialScale;
    Material bladeMaterial;
    Light bladeLight;
    AudioSource audioSource;
    public AudioClip powerOnSound;
    public AudioClip powerOffSound;
    Transform saberRoot;
    [Min(0f)] public float mouseRotationSensitivity = 0.2f;
    bool initialized;
    float motionDelayRemaining;
    readonly System.Collections.Generic.List<Object> owned = new System.Collections.Generic.List<Object>();

    void Awake() { Initialize(); }

    public void Initialize()
    {
        if (initialized || blade == null) return;
        initialized = true;
        initialScale = transform.localScale;
        saberRoot = transform.parent;
        bladeMaterial = new Material(blade.GetComponent<Renderer>().sharedMaterial);
        owned.Add(bladeMaterial);
        blade.GetComponent<Renderer>().sharedMaterial = bladeMaterial;
        bladeMaterial.SetFloat("_OuterGlow", 15f);
        BuildHilt();
        BuildStage();
        var lightObject = new GameObject("Blade illumination");
        lightObject.transform.SetParent(saberRoot, false);
        lightObject.transform.localPosition = new Vector3(0, 0.2f, -0.15f);
        bladeLight = lightObject.AddComponent<Light>();
        bladeLight.type = LightType.Point;
        bladeLight.range = 3f;
        bladeLight.shadows = LightShadows.None;
        audioSource = saberRoot.gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.22f;
        SetColor(0);
        TargetOn = startOn;
        Extension = startOn ? 1f : 0f;
        ApplyExtension();
        if (Application.isPlaying && startOn) PlayPowerSound(true);
    }

    public void Toggle()
    {
        TargetOn = !TargetOn;
        motionDelayRemaining = Mathf.Max(0f, TargetOn ? openingDelaySeconds : closingDelaySeconds);
        PlayPowerSound(TargetOn);
    }

    public void SetColor(int index)
    {
        colorIndex = (index % colors.Length + colors.Length) % colors.Length;
        bladeMaterial.SetColor("_BladeColor", BladeColor);
        if (bladeLight != null) bladeLight.color = BladeColor;
    }

    // MoveTowards uses elapsed time and allows reversing in the middle of a transition.
    public void Advance(float deltaTime)
    {
        float motionTime = Mathf.Max(0f, deltaTime);
        if (motionDelayRemaining > 0f)
        {
            float waitTime = Mathf.Min(motionDelayRemaining, motionTime);
            motionDelayRemaining -= waitTime;
            motionTime -= waitTime;
        }
        Extension = Mathf.MoveTowards(Extension, TargetOn ? 1f : 0f,
            motionTime / Mathf.Max(0.02f, transitionSeconds));
        ApplyExtension();
    }

    void ApplyExtension()
    {
        transform.localScale = new Vector3(initialScale.x, initialScale.y * Extension, initialScale.z);
        blade.SetActive(Extension > 0f);
        bladeLight.enabled = Extension > 0f;
        bladeLight.intensity = 2.5f * Extension;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.spaceKey.wasPressedThisFrame) Toggle();
            if (keyboard.cKey.wasPressedThisFrame) SetColor(colorIndex + 1);
        }
        Advance(Time.deltaTime);
        var mouse = Mouse.current;
        if (keyboard != null && keyboard.sKey.isPressed && mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            Camera camera = Camera.main;
            Vector3 tiltAxis = camera != null ? camera.transform.right : Vector3.right;
            Vector3 rollAxis = camera != null ? camera.transform.forward : Vector3.forward;
            // Mouse delta is already displacement per frame; do not multiply by deltaTime.
            Quaternion rotation = Quaternion.AngleAxis(-delta.x * mouseRotationSensitivity, rollAxis)
                * Quaternion.AngleAxis(delta.y * mouseRotationSensitivity, tiltAxis);
            saberRoot.rotation = rotation * saberRoot.rotation;
        }
    }

    void BuildHilt()
    {
        Material metal = LitMaterial(new Color(0.26f, 0.3f, 0.36f), 0.85f);
        Material grip = LitMaterial(new Color(0.025f, 0.03f, 0.04f), 0.2f);
        Part("Hilt", PrimitiveType.Cylinder, new Vector3(0,-0.88f,0), new Vector3(0.095f,0.17f,0.095f), metal);
        Part("Emitter collar", PrimitiveType.Cylinder, new Vector3(0,-0.7f,0), new Vector3(0.13f,0.035f,0.13f), metal);
        Part("Pommel", PrimitiveType.Cylinder, new Vector3(0,-1.06f,0), new Vector3(0.115f,0.025f,0.115f), metal);
        for (int i = 0; i < 6; i++)
            Part("Grip ring " + (i+1), PrimitiveType.Cylinder, new Vector3(0,-0.78f-i*0.045f,0),
                new Vector3(0.102f,0.012f,0.102f), grip);
        Part("Power switch", PrimitiveType.Sphere, new Vector3(0,-0.76f,-0.055f), Vector3.one * 0.035f, metal);
    }

    Material LitMaterial(Color color, float metallic)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", 0.65f);
        owned.Add(material);
        return material;
    }

    GameObject Part(string label, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(shape);
        part.name = label;
        part.transform.SetParent(saberRoot, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        var collider = part.GetComponent<Collider>();
        if (collider != null) { if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider); }
        return part;
    }

    void BuildStage()
    {
        var stage = new GameObject("Display stage");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(stage.transform, false);
        floor.transform.position = new Vector3(0, -1.15f, 0);
        floor.transform.localScale = new Vector3(12, 0.1f, 12);
        floor.GetComponent<Renderer>().sharedMaterial = LitMaterial(new Color(0.055f,0.065f,0.09f), 0.35f);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Backdrop";
        wall.transform.SetParent(stage.transform, false);
        wall.transform.position = new Vector3(0, 1, 1.2f);
        wall.transform.localScale = new Vector3(12, 6, 0.1f);
        wall.GetComponent<Renderer>().sharedMaterial = LitMaterial(new Color(0.035f,0.045f,0.07f), 0.1f);
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0, 0.2f, -3.5f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0,-0.15f,0) - camera.transform.position);
            camera.fieldOfView = 38;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f,0.015f,0.025f);
        }
        foreach (var sceneLight in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (sceneLight.type == LightType.Directional) sceneLight.intensity = 0.35f;
    }

    void PlayPowerSound(bool powerOn)
    {
        if (!Application.isPlaying || audioSource == null) return;
        audioSource.Stop();
        audioSource.clip = powerOn ? powerOnSound : powerOffSound;
        if (audioSource.clip != null) audioSource.Play();
    }

    void OnDestroy()
    {
        foreach (var item in owned)
            if (item != null) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
    }
}
