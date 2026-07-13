using UnityEngine;

/// <summary>
/// Rayo de Odin en 3D: sale disparado hacia donde MIRAS,
/// iluminando el campo, y fulmina al primer draugr que toca.
/// </summary>
public class Proyectil : MonoBehaviour
{
    const float Velocidad = 18f;
    const float Dano = 60f;

    private Vector3 direccion;
    private float vidaRestante = 1.4f;

    public static void Crear(Vector3 pos, Vector3 direccion)
    {
        GameObject go = new GameObject("RayoDeOdin");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(direccion);
        Proyectil p = go.AddComponent<Proyectil>();
        p.direccion = direccion.normalized;

        // nucleo + halo electrico (a lo largo del eje de vuelo)
        Transform halo = ConstructorPersonaje.Rect(go.transform, "Halo", Vector2.zero, new Vector2(0.34f, 0.34f), new Color(1f, 0.9f, 0.2f, 0.45f), 0, 1.1f, true);
        Transform rayo = ConstructorPersonaje.Rect(go.transform, "Rayo", Vector2.zero, new Vector2(0.14f, 0.14f), new Color(1f, 0.95f, 0.4f), 0, 0.9f, true);
        Transform nucleo = ConstructorPersonaje.Rect(go.transform, "Nucleo", Vector2.zero, new Vector2(0.06f, 0.06f), Color.white, 0, 0.7f, true);
        halo.localPosition = rayo.localPosition = nucleo.localPosition = Vector3.zero;

        Light luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.9f, 0.3f);
        luz.range = 9f;
        luz.intensity = 4f;
    }

    void Update()
    {
        transform.position += direccion * Velocidad * Time.deltaTime;
        float palpito = 1f + Mathf.Sin(Time.time * 55f) * 0.2f;
        transform.localScale = new Vector3(palpito, palpito, 1f);

        vidaRestante -= Time.deltaTime;
        if (vidaRestante <= 0f) { Destroy(gameObject); return; }

        foreach (Enemigo e in Enemigo.Todos)
        {
            if (e == null || e.muerto) continue;
            Vector3 d = (e.transform.position + Vector3.up * 1.1f) - transform.position;
            if (d.magnitude < 1.1f)
            {
                e.RecibirDano(Dano, transform.position - direccion);
                GestorAventura.Instancia.SonidoImpacto();
                Destroy(gameObject);
                return;
            }
        }
    }
}
