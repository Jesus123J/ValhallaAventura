using UnityEngine;

/// <summary>
/// Construye personajes 2D estilo "titere" (vista lateral) con rectangulos de color:
/// cada parte del cuerpo es un sprite y los brazos/piernas cuelgan de pivotes
/// para poder animarlos por codigo (girandolos en el eje Z).
/// - Vikingo (Bjorn): tunica azul, barba roja, casco con cuerno y ESPADA brillante.
/// - Draugr: piel verdosa, harapos, ojo rojo brillante y espada oxidada.
/// </summary>
public static class ConstructorPersonaje
{
    static Sprite spriteBlanco;
    static Sprite spriteCirculo;

    /// <summary>Sprite blanco de 1x1 unidad: se colorea y escala para dibujar todo.</summary>
    public static Sprite Blanco()
    {
        if (spriteBlanco == null)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            spriteBlanco = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }
        return spriteBlanco;
    }

    /// <summary>Sprite circular de 1 unidad de diametro (luna, brillos, nodos).</summary>
    public static Sprite Circulo()
    {
        if (spriteCirculo == null)
        {
            const int lado = 64;
            var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
            var px = new Color32[lado * lado];
            float r = lado / 2f;
            for (int y = 0; y < lado; y++)
                for (int x = 0; x < lado; x++)
                {
                    float dx = x - r + 0.5f, dy = y - r + 0.5f;
                    px[y * lado + x] = (dx * dx + dy * dy <= r * r)
                        ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            spriteCirculo = Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), lado);
        }
        return spriteCirculo;
    }

    /// <summary>Crea un rectangulo de color como hijo de 'padre'.</summary>
    public static Transform Rect(Transform padre, string nombre, Vector2 pos, Vector2 tam, Color color, int orden)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(tam.x, tam.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Blanco();
        sr.color = color;
        sr.sortingOrder = orden;
        return go.transform;
    }

    /// <summary>Igual que Rect pero circular.</summary>
    public static Transform Circ(Transform padre, string nombre, Vector2 pos, float diametro, Color color, int orden)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = Vector3.one * diametro;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Circulo();
        sr.color = color;
        sr.sortingOrder = orden;
        return go.transform;
    }

    /// <summary>Construye el titere completo y devuelve su animador configurado.</summary>
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

        // ---- Brazo de atras (detras del torso) ----
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
        Rect(visual, "Torso", new Vector2(0f, 1.10f), new Vector2(0.60f, 0.82f), ropa, 4);
        Rect(visual, "Cinturon", new Vector2(0f, 0.80f), new Vector2(0.62f, 0.14f), cuero, 5);
        Rect(visual, "Cabeza", new Vector2(0.02f, 1.78f), new Vector2(0.48f, 0.46f), piel, 5);

        if (esDraugr)
        {
            // ojo rojo brillante con halo
            Circ(visual, "OjoBrillo", new Vector2(0.15f, 1.82f), 0.30f, new Color(1f, 0.2f, 0.1f, 0.35f), 5);
            Rect(visual, "Ojo", new Vector2(0.15f, 1.82f), new Vector2(0.14f, 0.10f), new Color(1f, 0.25f, 0.1f), 6);
            Rect(visual, "Harapo", new Vector2(0f, 1.52f), new Vector2(0.66f, 0.16f), new Color(0.30f, 0.28f, 0.22f), 5);
        }
        else
        {
            Rect(visual, "Ojo", new Vector2(0.15f, 1.84f), new Vector2(0.09f, 0.09f), new Color(0.15f, 0.12f, 0.10f), 6);
            Rect(visual, "Barba", new Vector2(0.14f, 1.60f), new Vector2(0.34f, 0.24f), pelo, 6);
            Rect(visual, "Casco", new Vector2(0.02f, 2.02f), new Vector2(0.54f, 0.18f), metal, 6);
            Rect(visual, "Cuerno", new Vector2(-0.28f, 2.14f), new Vector2(0.12f, 0.30f), hueso, 6);
        }

        // ---- Brazo delantero con la ESPADA ----
        Transform brazoFrente = new GameObject("BrazoFrente").transform;
        brazoFrente.SetParent(visual, false);
        brazoFrente.localPosition = new Vector3(0.10f, 1.38f, 0f);
        Rect(brazoFrente, "Brazo", new Vector2(0f, -0.30f), new Vector2(0.18f, 0.60f), piel, 6);

        Transform espada = new GameObject("Espada").transform;
        espada.SetParent(brazoFrente, false);
        espada.localPosition = new Vector3(0f, -0.62f, 0f);
        Rect(espada, "Mango", new Vector2(0.10f, 0f), new Vector2(0.20f, 0.07f), cuero, 6);
        Rect(espada, "Guarda", new Vector2(0.20f, 0f), new Vector2(0.06f, 0.28f), esDraugr ? cuero : new Color(0.9f, 0.75f, 0.2f), 7);
        Rect(espada, "Hoja", new Vector2(0.62f, 0f), new Vector2(0.80f, 0.13f), metal, 7);
        if (!esDraugr)
            Rect(espada, "Brillo", new Vector2(0.62f, 0.03f), new Vector2(0.70f, 0.04f), new Color(1f, 1f, 1f, 0.8f), 8);

        AnimadorPersonaje anim = visual.gameObject.AddComponent<AnimadorPersonaje>();
        anim.brazoDer = brazoFrente;
        anim.brazoIzq = brazoAtras;
        anim.piernaDer = piernaFrente;
        anim.piernaIzq = piernaAtras;
        return anim;
    }
}
