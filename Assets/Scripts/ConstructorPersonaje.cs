using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Construccion 2.5D: el juego se juega en un plano lateral (X/Y) pero TODO
/// se dibuja con geometria 3D real (cubos y esferas con luz y sombra).
/// El parametro "orden" (antes orden de sprite) ahora se convierte en una
/// pequena separacion en Z: mayor orden = mas cerca de la camara.
/// </summary>
public static class ConstructorPersonaje
{
    const float PasoZ = 0.12f;          // separacion entre "capas"
    const float ProfundidadBase = 0.24f; // grosor por defecto de cada pieza

    static readonly Dictionary<Color, Material> materiales = new Dictionary<Color, Material>();
    static readonly Dictionary<Color, Material> materialesEmisivos = new Dictionary<Color, Material>();

    public static Material Mat(Color c)
    {
        if (!materiales.TryGetValue(c, out Material m))
        {
            m = new Material(Shader.Find("Standard"));
            m.color = c;
            if (c.a < 0.999f) HacerTransparente(m);
            materiales[c] = m;
        }
        return m;
    }

    /// <summary>Material que brilla por si mismo (runas, portal, rayos, luna).</summary>
    public static Material MatEmisivo(Color c)
    {
        if (!materialesEmisivos.TryGetValue(c, out Material m))
        {
            m = new Material(Shader.Find("Standard"));
            m.color = c;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * 0.9f);
            if (c.a < 0.999f) HacerTransparente(m);
            materialesEmisivos[c] = m;
        }
        return m;
    }

    static void HacerTransparente(Material m)
    {
        m.SetFloat("_Mode", 3f); // Transparent
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.renderQueue = 3000;
    }

    /// <summary>Crea un CUBO 3D (antes era un rectangulo 2D). Misma firma + profundidad opcional.</summary>
    public static Transform Rect(Transform padre, string nombre, Vector2 pos, Vector2 tam, Color color,
                                 int orden, float profundidad = ProfundidadBase, bool emisivo = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<BoxCollider>()); // solo visual
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, -orden * PasoZ);
        go.transform.localScale = new Vector3(tam.x, tam.y, profundidad);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = emisivo ? MatEmisivo(color) : Mat(color);
        if (color.a <= 0.001f) mr.enabled = false; // muros invisibles
        return go.transform;
    }

    /// <summary>Crea una ESFERA 3D achatada (halos, luna, brillos).</summary>
    public static Transform Circ(Transform padre, string nombre, Vector2 pos, float diametro, Color color,
                                 int orden, bool emisivo = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(go.GetComponent<SphereCollider>());
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, -orden * PasoZ);
        go.transform.localScale = new Vector3(diametro, diametro, diametro * 0.45f);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = emisivo ? MatEmisivo(color) : Mat(color);
        return go.transform;
    }

    /// <summary>Construye el titere voxel completo y devuelve su animador configurado.</summary>
    public static AnimadorPersonaje CrearVisual(Transform raiz, bool esDraugr)
    {
        Color piel   = esDraugr ? new Color(0.55f, 0.68f, 0.50f) : new Color(0.95f, 0.78f, 0.62f);
        Color ropa   = esDraugr ? new Color(0.22f, 0.25f, 0.22f) : new Color(0.20f, 0.35f, 0.65f);
        Color pelo   = esDraugr ? new Color(0.80f, 0.86f, 0.90f) : new Color(0.85f, 0.40f, 0.12f);
        Color pierna = esDraugr ? new Color(0.16f, 0.18f, 0.16f) : new Color(0.35f, 0.25f, 0.16f);
        Color metal  = esDraugr ? new Color(0.55f, 0.50f, 0.38f) : new Color(0.85f, 0.88f, 0.95f);
        Color cuero  = new Color(0.35f, 0.25f, 0.15f);
        Color hueso  = new Color(0.95f, 0.93f, 0.85f);

        Transform visual = new GameObject("Visual").transform;
        visual.SetParent(raiz, false);

        // ---- Brazo de atras ----
        Transform brazoAtras = new GameObject("BrazoAtras").transform;
        brazoAtras.SetParent(visual, false);
        brazoAtras.localPosition = new Vector3(-0.06f, 1.38f, 0f);
        Rect(brazoAtras, "Brazo", new Vector2(0f, -0.30f), new Vector2(0.17f, 0.55f), piel * 0.85f, 1);

        // ---- Piernas ----
        Transform piernaAtras = new GameObject("PiernaAtras").transform;
        piernaAtras.SetParent(visual, false);
        piernaAtras.localPosition = new Vector3(-0.08f, 0.80f, 0f);
        Rect(piernaAtras, "Pierna", new Vector2(0f, -0.36f), new Vector2(0.22f, 0.72f), pierna * 0.8f, 2);

        Transform piernaFrente = new GameObject("PiernaFrente").transform;
        piernaFrente.SetParent(visual, false);
        piernaFrente.localPosition = new Vector3(0.10f, 0.80f, 0f);
        Rect(piernaFrente, "Pierna", new Vector2(0f, -0.36f), new Vector2(0.22f, 0.72f), pierna, 3);

        // ---- Torso, cinturon, cabeza ----
        Rect(visual, "Torso", new Vector2(0f, 1.10f), new Vector2(0.60f, 0.82f), ropa, 4, 0.34f);
        Rect(visual, "Cinturon", new Vector2(0f, 0.80f), new Vector2(0.62f, 0.14f), cuero, 5, 0.36f);
        Rect(visual, "Cabeza", new Vector2(0.02f, 1.78f), new Vector2(0.48f, 0.46f), piel, 5, 0.4f);

        if (esDraugr)
        {
            Circ(visual, "OjoBrillo", new Vector2(0.15f, 1.82f), 0.30f, new Color(1f, 0.2f, 0.1f, 0.35f), 5);
            Rect(visual, "Ojo", new Vector2(0.15f, 1.82f), new Vector2(0.14f, 0.10f), new Color(1f, 0.25f, 0.1f), 6, 0.1f, true);
            Rect(visual, "Harapo", new Vector2(0f, 1.52f), new Vector2(0.66f, 0.16f), new Color(0.30f, 0.28f, 0.22f), 5, 0.42f);
        }
        else
        {
            Rect(visual, "Ojo", new Vector2(0.15f, 1.84f), new Vector2(0.09f, 0.09f), new Color(0.15f, 0.12f, 0.10f), 6, 0.1f);
            Rect(visual, "Barba", new Vector2(0.14f, 1.60f), new Vector2(0.34f, 0.24f), pelo, 6, 0.3f);
            Rect(visual, "Casco", new Vector2(0.02f, 2.02f), new Vector2(0.54f, 0.18f), metal, 6, 0.44f);
            Rect(visual, "Cuerno", new Vector2(-0.28f, 2.14f), new Vector2(0.12f, 0.30f), hueso, 6, 0.12f);
        }

        // ---- Brazo delantero con la ESPADA ----
        Transform brazoFrente = new GameObject("BrazoFrente").transform;
        brazoFrente.SetParent(visual, false);
        brazoFrente.localPosition = new Vector3(0.10f, 1.38f, 0f);
        Rect(brazoFrente, "Brazo", new Vector2(0f, -0.30f), new Vector2(0.18f, 0.60f), piel, 6);

        Transform espada = new GameObject("Espada").transform;
        espada.SetParent(brazoFrente, false);
        espada.localPosition = new Vector3(0f, -0.62f, 0f);
        Rect(espada, "Mango", new Vector2(0.10f, 0f), new Vector2(0.20f, 0.07f), cuero, 6, 0.1f);
        Rect(espada, "Guarda", new Vector2(0.20f, 0f), new Vector2(0.06f, 0.28f), esDraugr ? cuero : new Color(0.9f, 0.75f, 0.2f), 7, 0.12f);
        Rect(espada, "Hoja", new Vector2(0.62f, 0f), new Vector2(0.80f, 0.13f), metal, 7, 0.06f);
        if (!esDraugr)
            Rect(espada, "Brillo", new Vector2(0.62f, 0.03f), new Vector2(0.70f, 0.04f), new Color(1f, 1f, 1f, 0.8f), 8, 0.04f, true);

        AnimadorPersonaje anim = visual.gameObject.AddComponent<AnimadorPersonaje>();
        anim.brazoDer = brazoFrente;
        anim.brazoIzq = brazoAtras;
        anim.piernaDer = piernaFrente;
        anim.piernaIzq = piernaAtras;
        return anim;
    }
}
