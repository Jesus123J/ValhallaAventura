using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Rayo de Odin con 3 NIVELES (se mejoran en la tienda):
/// - Nivel 1: rayo directo (60 de dano al primer enemigo).
/// - Nivel 2: ATRAVIESA a todos los enemigos en su camino.
/// - Nivel 3: ademas, al tocar cae un TRUENO DEL CIELO que dana en area.
/// </summary>
public class Proyectil : MonoBehaviour
{
    const float Velocidad = 18f;
    const float Dano = 60f;

    private Vector3 direccion;
    private int nivel = 1;
    private float vidaRestante = 1.4f;
    private readonly HashSet<Enemigo> yaGolpeados = new HashSet<Enemigo>();

    public static void Crear(Vector3 pos, Vector3 direccion, int nivel)
    {
        GameObject go = new GameObject("RayoDeOdin");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(direccion);
        Proyectil p = go.AddComponent<Proyectil>();
        p.direccion = direccion.normalized;
        p.nivel = Mathf.Clamp(nivel, 1, 3);

        float grosor = 1f + (p.nivel - 1) * 0.25f; // mas nivel = rayo mas grueso
        Transform halo = ConstructorPersonaje.Rect(go.transform, "Halo", Vector2.zero, new Vector2(0.34f * grosor, 0.34f * grosor), new Color(1f, 0.9f, 0.2f, 0.45f), 0, 1.1f, true);
        Transform rayo = ConstructorPersonaje.Rect(go.transform, "Rayo", Vector2.zero, new Vector2(0.14f * grosor, 0.14f * grosor), new Color(1f, 0.95f, 0.4f), 0, 0.9f, true);
        Transform nucleo = ConstructorPersonaje.Rect(go.transform, "Nucleo", Vector2.zero, new Vector2(0.06f, 0.06f), Color.white, 0, 0.7f, true);
        halo.localPosition = rayo.localPosition = nucleo.localPosition = Vector3.zero;

        Light luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.9f, 0.3f);
        luz.range = 9f + p.nivel * 2f;
        luz.intensity = 3.5f + p.nivel;
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
            if (e == null || e.muerto || yaGolpeados.Contains(e)) continue;
            Vector3 d = (e.transform.position + Vector3.up * 1.1f) - transform.position;
            if (d.magnitude < 1.1f)
            {
                yaGolpeados.Add(e);
                e.RecibirDano(Dano, transform.position - direccion, 0.8f);
                GestorAventura.Instancia.SonidoImpacto();

                if (nivel >= 3)
                    TruenoDelCielo(e.transform.position);

                if (nivel < 2) // nivel 1: se consume en el primer golpe
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }

    /// <summary>Nivel 3: columna de trueno que cae del cielo y dana alrededor.</summary>
    void TruenoDelCielo(Vector3 punto)
    {
        GameObject columna = new GameObject("Trueno");
        columna.transform.position = punto;
        Transform luz1 = ConstructorPersonaje.Rect(columna.transform, "Columna", Vector2.zero, new Vector2(0.5f, 30f), new Color(1f, 0.95f, 0.5f, 0.8f), 0, 0.5f, true);
        luz1.localPosition = new Vector3(0f, 15f, 0f);

        Light destello = columna.AddComponent<Light>();
        destello.type = LightType.Point;
        destello.color = new Color(1f, 0.95f, 0.5f);
        destello.range = 14f;
        destello.intensity = 7f;

        foreach (Enemigo e in Enemigo.Todos)
        {
            if (e == null || e.muerto) continue;
            Vector3 d = e.transform.position - punto;
            d.y = 0f;
            if (d.magnitude < 3.5f && !yaGolpeados.Contains(e))
            {
                yaGolpeados.Add(e);
                e.RecibirDano(40f, punto, 1f);
            }
        }

        GestorAventura.Instancia.SonidoTrueno();
        Destroy(columna, 0.25f);
    }
}
