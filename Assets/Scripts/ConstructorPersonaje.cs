using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Fabrica de geometria y personajes voxel.
/// Los DRAUGR ahora tienen cuerpo completo: torso en dos piezas, hombreras,
/// brazos con antebrazo y garras, piernas con espinilla y pies, capa rota,
/// mandibula y dos ojos rojos brillantes, con postura encorvada.
/// </summary>
public static class ConstructorPersonaje
{
    const float PasoZ = 0.12f;
    const float ProfundidadBase = 0.24f;

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
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.renderQueue = 3000;
    }

    public static Transform Rect(Transform padre, string nombre, Vector2 pos, Vector2 tam, Color color,
                                 int orden, float profundidad = ProfundidadBase, bool emisivo = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, -orden * PasoZ);
        go.transform.localScale = new Vector3(tam.x, tam.y, profundidad);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = emisivo ? MatEmisivo(color) : Mat(color);
        if (color.a <= 0.001f) mr.enabled = false;
        return go.transform;
    }

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

    /// <summary>Cubo 3D con posicion y tamano completos (x,y,z). Para armar cuerpos.</summary>
    public static Transform Cubo(Transform padre, string nombre, Vector3 pos, Vector3 tam, Color color, bool emisivo = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = tam;
        go.GetComponent<MeshRenderer>().sharedMaterial = emisivo ? MatEmisivo(color) : Mat(color);
        return go.transform;
    }

    /// <summary>Construye el cuerpo voxel completo y devuelve su animador.</summary>
    public static AnimadorPersonaje CrearVisual(Transform raiz, bool esDraugr)
    {
        Color piel    = esDraugr ? new Color(0.52f, 0.64f, 0.48f) : new Color(0.95f, 0.78f, 0.62f);
        Color pielOsc = piel * 0.82f;
        Color ropa    = esDraugr ? new Color(0.20f, 0.23f, 0.20f) : new Color(0.20f, 0.35f, 0.65f);
        Color ropaOsc = ropa * 0.75f;
        Color pierna  = esDraugr ? new Color(0.15f, 0.17f, 0.15f) : new Color(0.35f, 0.25f, 0.16f);
        Color metal   = esDraugr ? new Color(0.5f, 0.46f, 0.36f) : new Color(0.8f, 0.83f, 0.9f);
        Color cuero   = new Color(0.35f, 0.25f, 0.15f);
        Color hueso   = new Color(0.93f, 0.91f, 0.83f);
        Color pelo    = esDraugr ? new Color(0.78f, 0.84f, 0.88f) : new Color(0.85f, 0.40f, 0.12f);

        Transform visual = new GameObject("Visual").transform;
        visual.SetParent(raiz, false);

        // postura: los draugr van encorvados hacia adelante
        Transform postura = new GameObject("Postura").transform;
        postura.SetParent(visual, false);
        if (esDraugr) postura.localRotation = Quaternion.Euler(10f, 0f, 0f);

        // ================= PIERNAS (muslo + espinilla + pie) =================
        Transform piernaIzq = CrearPierna(postura, "PiernaIzq", -0.15f, pierna, pielOsc, esDraugr);
        Transform piernaDer = CrearPierna(postura, "PiernaDer", 0.15f, pierna, pielOsc, esDraugr);

        // ================= TORSO en dos piezas =================
        Cubo(postura, "Abdomen", new Vector3(0f, 1.0f, 0f), new Vector3(0.46f, 0.32f, 0.32f), ropaOsc);
        Cubo(postura, "Pecho", new Vector3(0f, 1.32f, 0f), new Vector3(0.62f, 0.42f, 0.40f), ropa);
        Cubo(postura, "Cinturon", new Vector3(0f, 0.84f, 0f), new Vector3(0.5f, 0.1f, 0.36f), cuero);
        // hombreras
        Cubo(postura, "HombreraIzq", new Vector3(-0.40f, 1.5f, 0f), new Vector3(0.24f, 0.15f, 0.32f), esDraugr ? cuero : metal);
        Cubo(postura, "HombreraDer", new Vector3(0.40f, 1.5f, 0f), new Vector3(0.24f, 0.15f, 0.32f), esDraugr ? cuero : metal);

        if (esDraugr)
        {
            // capa rota a la espalda
            Cubo(postura, "Capa", new Vector3(0f, 1.15f, -0.26f), new Vector3(0.55f, 0.8f, 0.05f), new Color(0.16f, 0.14f, 0.12f));
            Cubo(postura, "CapaRota", new Vector3(-0.15f, 0.68f, -0.26f), new Vector3(0.2f, 0.22f, 0.05f), new Color(0.16f, 0.14f, 0.12f));
        }

        // ================= CABEZA =================
        Cubo(postura, "Cuello", new Vector3(0f, 1.58f, 0f), new Vector3(0.16f, 0.1f, 0.16f), piel);
        Transform cabeza = Cubo(postura, "Cabeza", new Vector3(0f, 1.85f, 0f), new Vector3(0.42f, 0.4f, 0.42f), piel);

        if (esDraugr)
        {
            // dos ojos rojos brillantes
            Cubo(cabeza, "OjoIzq", new Vector3(-0.24f, 0.1f, 0.52f), new Vector3(0.28f, 0.2f, 0.1f), new Color(1f, 0.25f, 0.1f), true);
            Cubo(cabeza, "OjoDer", new Vector3(0.24f, 0.1f, 0.52f), new Vector3(0.28f, 0.2f, 0.1f), new Color(1f, 0.25f, 0.1f), true);
            Cubo(cabeza, "Mandibula", new Vector3(0f, -0.42f, 0.15f), new Vector3(0.8f, 0.22f, 0.75f), pielOsc);
            Cubo(cabeza, "PeloColgante", new Vector3(0f, 0.1f, -0.5f), new Vector3(0.9f, 0.9f, 0.15f), pelo);
        }
        else
        {
            Cubo(cabeza, "OjoIzq", new Vector3(-0.22f, 0.1f, 0.52f), new Vector3(0.18f, 0.16f, 0.1f), new Color(0.15f, 0.12f, 0.1f));
            Cubo(cabeza, "OjoDer", new Vector3(0.22f, 0.1f, 0.52f), new Vector3(0.18f, 0.16f, 0.1f), new Color(0.15f, 0.12f, 0.1f));
            Cubo(cabeza, "Nariz", new Vector3(0f, -0.1f, 0.55f), new Vector3(0.16f, 0.2f, 0.15f), pielOsc);
            Cubo(cabeza, "Barba", new Vector3(0f, -0.45f, 0.3f), new Vector3(0.75f, 0.45f, 0.5f), pelo);
            Cubo(cabeza, "Casco", new Vector3(0f, 0.42f, 0f), new Vector3(1.12f, 0.4f, 1.12f), metal);
            Cubo(cabeza, "CuernoIzq", new Vector3(-0.68f, 0.65f, 0f), new Vector3(0.22f, 0.65f, 0.22f), hueso);
            Cubo(cabeza, "CuernoDer", new Vector3(0.68f, 0.65f, 0f), new Vector3(0.22f, 0.65f, 0.22f), hueso);
        }

        // ================= BRAZOS (brazo + antebrazo + mano/garra) =================
        Transform brazoIzq = CrearBrazo(postura, "BrazoIzq", -0.46f, piel, pielOsc, ropa, esDraugr, false, metal, cuero);
        Transform brazoDer = CrearBrazo(postura, "BrazoDer", 0.46f, piel, pielOsc, ropa, esDraugr, true, metal, cuero);

        AnimadorPersonaje anim = visual.gameObject.AddComponent<AnimadorPersonaje>();
        anim.brazoDer = brazoDer;
        anim.brazoIzq = brazoIzq;
        anim.piernaDer = piernaDer;
        anim.piernaIzq = piernaIzq;
        return anim;
    }

    static Transform CrearPierna(Transform padre, string nombre, float x, Color pantalon, Color pielOsc, bool esDraugr)
    {
        Transform pivote = new GameObject(nombre).transform;
        pivote.SetParent(padre, false);
        pivote.localPosition = new Vector3(x, 0.85f, 0f);
        Cubo(pivote, "Muslo", new Vector3(0f, -0.22f, 0f), new Vector3(0.22f, 0.42f, 0.24f), pantalon);
        Cubo(pivote, "Espinilla", new Vector3(0f, -0.6f, 0f), new Vector3(0.18f, 0.4f, 0.2f), pantalon * 0.85f);
        Cubo(pivote, "Pie", new Vector3(0f, -0.82f, 0.06f), new Vector3(0.2f, 0.1f, 0.34f), esDraugr ? pielOsc : new Color(0.25f, 0.17f, 0.1f));
        return pivote;
    }

    static Transform CrearBrazo(Transform padre, string nombre, float x, Color piel, Color pielOsc,
                                Color ropa, bool esDraugr, bool derecho, Color metal, Color cuero)
    {
        Transform pivote = new GameObject(nombre).transform;
        pivote.SetParent(padre, false);
        pivote.localPosition = new Vector3(x, 1.48f, 0f);
        Cubo(pivote, "Brazo", new Vector3(0f, -0.2f, 0f), new Vector3(0.17f, 0.38f, 0.2f), ropa);
        Cubo(pivote, "Antebrazo", new Vector3(0f, -0.52f, 0f), new Vector3(0.15f, 0.32f, 0.17f), piel);
        Transform mano = Cubo(pivote, "Mano", new Vector3(0f, -0.74f, 0f), new Vector3(0.16f, 0.14f, 0.18f), pielOsc);

        if (esDraugr)
        {
            // garras hacia adelante
            for (int g = 0; g < 3; g++)
                Cubo(mano, "Garra_" + g, new Vector3(-0.25f + g * 0.25f, -0.55f, 0.3f), new Vector3(0.18f, 0.45f, 0.18f), new Color(0.85f, 0.83f, 0.75f));
        }

        if (derecho)
        {
            // espada en la mano derecha, hoja hacia adelante
            Transform espada = new GameObject("Espada").transform;
            espada.SetParent(pivote, false);
            espada.localPosition = new Vector3(0f, -0.74f, 0.1f);
            Cubo(espada, "Mango", new Vector3(0f, 0f, -0.12f), new Vector3(0.06f, 0.06f, 0.3f), cuero);
            Cubo(espada, "Guarda", new Vector3(0f, 0f, 0.06f), new Vector3(0.26f, 0.06f, 0.06f), esDraugr ? cuero : new Color(0.9f, 0.75f, 0.2f));
            Cubo(espada, "Hoja", new Vector3(0f, 0f, 0.5f), new Vector3(0.1f, 0.035f, 0.85f), metal);
        }
        return pivote;
    }
}
