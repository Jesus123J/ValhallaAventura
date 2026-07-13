using UnityEngine;
using System.Collections;

/// <summary>
/// Cofre del tesoro: al acercarte se abre solo (la tapa gira) y suelta
/// un premio aleatorio: lluvia de almas, una pocion o una GEMA rara (+150 almas).
/// </summary>
public class Cofre : MonoBehaviour
{
    private bool abierto;
    private Transform tapa;

    public static Cofre Crear(Vector3 pos)
    {
        GameObject go = new GameObject("Cofre");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Cofre c = go.AddComponent<Cofre>();

        Color madera = new Color(0.45f, 0.30f, 0.16f);
        Color oro = new Color(0.95f, 0.78f, 0.25f);

        Transform cuerpo = ConstructorPersonaje.Rect(go.transform, "Cuerpo", new Vector2(0f, 0.25f), new Vector2(0.9f, 0.5f), madera, 0, 0.6f);
        ConstructorPersonaje.Rect(cuerpo, "Banda", new Vector2(0f, 0f), new Vector2(1.02f, 0.18f), oro, 1, 0.62f);

        c.tapa = new GameObject("PivoteTapa").transform;
        c.tapa.SetParent(go.transform, false);
        c.tapa.localPosition = new Vector3(0f, 0.5f, -0.3f); // bisagra trasera
        Transform tapaCubo = ConstructorPersonaje.Rect(c.tapa, "Tapa", Vector2.zero, new Vector2(0.9f, 0.22f), madera * 1.15f, 0, 0.6f);
        tapaCubo.localPosition = new Vector3(0f, 0.11f, 0.3f);
        ConstructorPersonaje.Rect(tapaCubo, "Cerradura", new Vector2(0f, -0.05f), new Vector2(0.16f, 0.2f), oro, 1, 0.64f);

        return c;
    }

    void Update()
    {
        if (abierto) return;
        Jugador j = GestorAventura.Instancia != null ? GestorAventura.Instancia.Bjorn : null;
        if (j == null) return;
        Vector3 d = j.transform.position - transform.position;
        d.y = 0f;
        if (d.magnitude < 1.8f)
        {
            abierto = true;
            StartCoroutine(Abrir());
        }
    }

    IEnumerator Abrir()
    {
        // la tapa gira hacia atras
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            tapa.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -110f, t / 0.4f), 0f, 0f);
            yield return null;
        }

        // premio aleatorio
        float suerte = Random.value;
        if (suerte < 0.55f)
        {
            // lluvia de almas alrededor del cofre
            for (int i = 0; i < 3; i++)
            {
                float ang = i * 2.1f;
                Recompensa.Crear(Recompensa.Tipo.Almas,
                    transform.position + new Vector3(Mathf.Cos(ang) * 1.2f, 1f, Mathf.Sin(ang) * 1.2f));
            }
            GestorAventura.Instancia.CofreAbierto("¡COFRE! Lluvia de almas");
        }
        else if (suerte < 0.85f)
        {
            Recompensa.Crear(Recompensa.Tipo.Pocion, transform.position + Vector3.up * 1f);
            GestorAventura.Instancia.CofreAbierto("¡COFRE! Hidromiel curativa");
        }
        else
        {
            GestorAventura.Instancia.CofreGema(); // gema rara: +150 almas directas
        }

        // brillo dorado al abrirse
        Light luz = gameObject.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.85f, 0.3f);
        luz.range = 6f;
        luz.intensity = 3f;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            luz.intensity = Mathf.Lerp(3f, 0f, t / 1.2f);
            yield return null;
        }
        Destroy(luz);
    }
}
