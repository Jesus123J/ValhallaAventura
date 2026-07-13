using UnityEngine;

/// <summary>
/// Rayo de Odin: proyectil electrico que cruza la pantalla
/// y fulmina al primer draugr que toca (60 de dano).
/// </summary>
public class Proyectil : MonoBehaviour
{
    const float Velocidad = 15f;
    const float Dano = 60f;

    private int direccion;
    private float vidaRestante = 1.1f;

    public static void Crear(Vector3 pos, int direccion)
    {
        GameObject go = new GameObject("RayoDeOdin");
        go.transform.position = pos;
        Proyectil p = go.AddComponent<Proyectil>();
        p.direccion = direccion;

        // nucleo blanco + halo electrico amarillo + chispas
        ConstructorPersonaje.Rect(go.transform, "Halo", Vector2.zero, new Vector2(1.1f, 0.34f), new Color(1f, 0.9f, 0.2f, 0.45f), 9);
        ConstructorPersonaje.Rect(go.transform, "Rayo", Vector2.zero, new Vector2(0.9f, 0.14f), new Color(1f, 0.95f, 0.4f), 10);
        ConstructorPersonaje.Rect(go.transform, "Nucleo", Vector2.zero, new Vector2(0.7f, 0.06f), Color.white, 11);
        ConstructorPersonaje.Rect(go.transform, "Chispa1", new Vector2(-0.35f, 0.14f), new Vector2(0.18f, 0.05f), new Color(1f, 0.95f, 0.4f), 10);
        ConstructorPersonaje.Rect(go.transform, "Chispa2", new Vector2(-0.15f, -0.13f), new Vector2(0.15f, 0.05f), new Color(1f, 0.95f, 0.4f), 10);
    }

    void Update()
    {
        transform.position += Vector3.right * direccion * Velocidad * Time.deltaTime;
        transform.localScale = new Vector3(1f + Mathf.Sin(Time.time * 60f) * 0.15f,
                                           1f + Mathf.Sin(Time.time * 47f) * 0.25f, 1f);

        vidaRestante -= Time.deltaTime;
        if (vidaRestante <= 0f) { Destroy(gameObject); return; }

        foreach (Enemigo e in Enemigo.Todos)
        {
            if (e == null || e.muerto) continue;
            float dx = Mathf.Abs(e.transform.position.x - transform.position.x);
            float dy = Mathf.Abs(e.transform.position.y + 1f - transform.position.y);
            if (dx < 0.9f && dy < 1.5f)
            {
                e.RecibirDano(Dano, transform.position - Vector3.right * direccion);
                GestorAventura.Instancia.SonidoImpacto();
                Destroy(gameObject);
                return;
            }
        }
    }
}
