using UnityEngine;

/// <summary>
/// Hoguera de CHECKPOINT: al acercarte se aviva, te cura el 50% de la vida
/// y se convierte en tu punto de reaparicion. La llama parpadea con luz calida.
/// </summary>
public class Hoguera : MonoBehaviour
{
    public static Vector3 PuntoRespawn;

    private bool activada;
    private Light luz;
    private Transform llama;
    private float intensidadBase = 1.1f;

    public static Hoguera Crear(Vector3 pos)
    {
        GameObject go = new GameObject("Hoguera");
        go.transform.position = pos;
        Hoguera h = go.AddComponent<Hoguera>();

        Color piedra = new Color(0.4f, 0.42f, 0.48f);
        Color lenoC = new Color(0.35f, 0.23f, 0.13f);

        // piedras alrededor
        for (int i = 0; i < 5; i++)
        {
            float ang = i * Mathf.PI * 2f / 5f;
            Transform p = ConstructorPersonaje.Rect(go.transform, "Piedra_" + i,
                Vector2.zero, new Vector2(0.28f, 0.22f), piedra, 0, 0.28f);
            p.localPosition = new Vector3(Mathf.Cos(ang) * 0.55f, 0.1f, Mathf.Sin(ang) * 0.55f);
            p.localRotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
        }
        // lenos cruzados
        Transform l1 = ConstructorPersonaje.Rect(go.transform, "Leno1", Vector2.zero, new Vector2(0.85f, 0.12f), lenoC, 0, 0.12f);
        l1.localPosition = new Vector3(0f, 0.15f, 0f);
        l1.localRotation = Quaternion.Euler(0f, 30f, 0f);
        Transform l2 = ConstructorPersonaje.Rect(go.transform, "Leno2", Vector2.zero, new Vector2(0.85f, 0.12f), lenoC, 0, 0.12f);
        l2.localPosition = new Vector3(0f, 0.15f, 0f);
        l2.localRotation = Quaternion.Euler(0f, -40f, 0f);

        // llama (dos cubos emisivos)
        h.llama = new GameObject("Llama").transform;
        h.llama.SetParent(go.transform, false);
        h.llama.localPosition = new Vector3(0f, 0.45f, 0f);
        Transform f1 = ConstructorPersonaje.Rect(h.llama, "Fuego", Vector2.zero, new Vector2(0.34f, 0.5f), new Color(1f, 0.45f, 0.1f), 0, 0.34f, true);
        f1.localRotation = Quaternion.Euler(0f, 45f, 0f);
        Transform f2 = ConstructorPersonaje.Rect(h.llama, "FuegoInterno", Vector2.zero, new Vector2(0.18f, 0.32f), new Color(1f, 0.85f, 0.3f), 1, 0.18f, true);
        f2.localPosition = new Vector3(0f, -0.05f, 0f);

        h.luz = go.AddComponent<Light>();
        h.luz.type = LightType.Point;
        h.luz.color = new Color(1f, 0.6f, 0.25f);
        h.luz.range = 9f;
        h.luz.intensity = h.intensidadBase;

        return h;
    }

    void Update()
    {
        // parpadeo de fuego
        float parpadeo = Mathf.PerlinNoise(Time.time * 6f, transform.position.x) * 0.5f;
        luz.intensity = intensidadBase + parpadeo;
        llama.localScale = Vector3.one * (1f + parpadeo * 0.25f);

        if (activada) return;
        Jugador j = GestorAventura.Instancia != null ? GestorAventura.Instancia.Bjorn : null;
        if (j == null) return;
        Vector3 d = j.transform.position - transform.position;
        d.y = 0f;
        if (d.magnitude < 2.2f)
        {
            activada = true;
            intensidadBase = 2.4f; // se aviva
            PuntoRespawn = transform.position + new Vector3(1.2f, 1.2f, 0f);
            j.vida = Mathf.Min(j.vidaMax, j.vida + j.vidaMax * 0.5f);
            GestorAventura.Instancia.HogueraEncendida();
        }
    }
}
