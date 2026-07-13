using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Draugr 2D: persigue a Bjorn por el suelo y ataca con su espada oxidada.
/// El JEFE (guardian del portal) es mas grande, mas duro y pega mas fuerte.
/// Al morir cae de espaldas, se hunde y suelta almas.
/// </summary>
public class Enemigo : MonoBehaviour
{
    public static readonly List<Enemigo> Todos = new List<Enemigo>();

    [HideInInspector] public float vida = 100f;
    [HideInInspector] public bool muerto;
    [HideInInspector] public bool esJefe;

    private float velocidad = 2.7f;
    private float dano = 10f;
    private Jugador jugador;
    private AnimadorPersonaje anim;
    private Transform visual;
    private float cooldown;

    void Awake()
    {
        anim = ConstructorPersonaje.CrearVisual(transform, true);
        visual = anim.transform;
        Todos.Add(this);
    }

    void OnDestroy() { Todos.Remove(this); }

    public void Inicializar(Jugador j, bool jefe)
    {
        jugador = j;
        esJefe = jefe;
        if (jefe)
        {
            transform.localScale = Vector3.one * 1.6f;
            vida = 300f;
            dano = 18f;
            velocidad = 2.2f;
            // corona de guardian y ojos mas brillantes
            ConstructorPersonaje.Rect(visual, "Corona", new Vector2(0.02f, 2.18f),
                                      new Vector2(0.5f, 0.14f), new Color(0.95f, 0.75f, 0.15f), 7);
        }
    }

    void Update()
    {
        if (muerto || jugador == null || !jugador.controlActivo)
        {
            if (!muerto && anim != null) anim.Caminando(false);
            return;
        }

        float dx = jugador.transform.position.x - transform.position.x;
        float dy = Mathf.Abs(jugador.transform.position.y - transform.position.y);
        float dist = Mathf.Abs(dx);
        cooldown -= Time.deltaTime;

        // mirar hacia el jugador
        visual.localScale = new Vector3(dx >= 0f ? 1f : -1f, 1f, 1f);

        float alcance = esJefe ? 1.9f : 1.4f;
        if (dist < 11f && dist > alcance)
        {
            transform.position += Vector3.right * Mathf.Sign(dx) * velocidad * Time.deltaTime;
            anim.Caminando(true);
        }
        else
        {
            anim.Caminando(false);
            if (dist <= alcance + 0.3f && dy < 1.6f && cooldown <= 0f)
            {
                cooldown = esJefe ? 1.6f : 1.25f;
                StartCoroutine(Atacar());
            }
        }
    }

    IEnumerator Atacar()
    {
        anim.Atacar();
        yield return new WaitForSeconds(0.15f);
        if (muerto || jugador == null) yield break;

        float dx = Mathf.Abs(jugador.transform.position.x - transform.position.x);
        float dy = Mathf.Abs(jugador.transform.position.y - transform.position.y);
        if (dx < (esJefe ? 2.4f : 1.9f) && dy < 1.7f)
            jugador.RecibirDano(dano);
    }

    public void RecibirDano(float d, Vector3 origen)
    {
        if (muerto) return;
        vida -= d;
        GestorAventura.Instancia.SonidoImpacto();

        // retroceso y sacudida
        float dir = Mathf.Sign(transform.position.x - origen.x);
        transform.position += new Vector3(dir * (esJefe ? 0.25f : 0.5f), 0f, 0f);
        StartCoroutine(Sacudida());

        if (vida <= 0f)
            StartCoroutine(Morir());
    }

    IEnumerator Sacudida()
    {
        Vector3 e = transform.localScale;
        transform.localScale = e * 1.1f;
        yield return new WaitForSeconds(0.06f);
        transform.localScale = e;
    }

    IEnumerator Morir()
    {
        muerto = true;
        GestorAventura.Instancia.EnemigoMuerto(this);

        Quaternion inicio = transform.rotation;
        Quaternion caido = Quaternion.Euler(0f, 0f, visual.localScale.x >= 0f ? 82f : -82f);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            transform.rotation = Quaternion.Slerp(inicio, caido, t / 0.35f);
            yield return null;
        }
        yield return new WaitForSeconds(0.5f);
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            transform.position += Vector3.down * 1.4f * Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }
}
