using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Draugr en 3D: te persigue por el campo, te rodea y ataca de frente.
/// El JEFE guardian es mas grande, resistente y pega mas fuerte.
/// Telegrafia el golpe (alza el arma) para que siempre puedas esquivar.
/// </summary>
public class Enemigo : MonoBehaviour
{
    public static readonly List<Enemigo> Todos = new List<Enemigo>();

    [HideInInspector] public float vida = 100f;
    [HideInInspector] public bool muerto;
    [HideInInspector] public bool esJefe;

    private float velocidad = 3f;
    private float dano = 10f;
    private float alcance = 1.9f;
    private Jugador jugador;
    private AnimadorPersonaje anim;
    private float cooldown;

    void Awake()
    {
        anim = ConstructorPersonaje.CrearVisual(transform, true);
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
            dano = 16f;
            velocidad = 2.5f;
            alcance = 2.6f;
            ConstructorPersonaje.Rect(anim.transform, "Corona", new Vector2(0.02f, 2.2f),
                                      new Vector2(0.5f, 0.14f), new Color(0.95f, 0.75f, 0.15f), 7, 0.44f, true);
        }
    }

    void Update()
    {
        if (muerto || jugador == null || !jugador.controlActivo)
        {
            if (!muerto && anim != null) anim.Caminando(false);
            return;
        }

        Vector3 hacia = jugador.transform.position - transform.position;
        hacia.y = 0f;
        float dist = hacia.magnitude;
        cooldown -= Time.deltaTime;

        if (dist < 18f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                                                  Quaternion.LookRotation(hacia), 8f * Time.deltaTime);

        if (dist < 16f && dist > alcance)
        {
            transform.position += hacia.normalized * velocidad * Time.deltaTime;
            anim.Caminando(true);
        }
        else
        {
            anim.Caminando(false);
            if (dist <= alcance + 0.4f && cooldown <= 0f)
            {
                cooldown = esJefe ? 1.7f : 1.3f;
                StartCoroutine(Atacar());
            }
        }
    }

    IEnumerator Atacar()
    {
        anim.Atacar(); // alza el arma: el jugador VE venir el golpe
        yield return new WaitForSeconds(0.22f);
        if (muerto || jugador == null) yield break;

        Vector3 hacia = jugador.transform.position - transform.position;
        hacia.y = 0f;
        if (hacia.magnitude < alcance + 0.7f)
            jugador.RecibirDano(dano);
    }

    public void RecibirDano(float d, Vector3 origen)
    {
        if (muerto) return;
        vida -= d;
        GestorAventura.Instancia.SonidoImpacto();

        Vector3 empuje = transform.position - origen;
        empuje.y = 0f;
        transform.position += empuje.normalized * (esJefe ? 0.25f : 0.55f);
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
        Quaternion caido = inicio * Quaternion.Euler(-88f, 0f, 0f); // cae de espaldas
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            transform.rotation = Quaternion.Slerp(inicio, caido, t / 0.35f);
            yield return null;
        }
        yield return new WaitForSeconds(0.5f);
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            transform.position += Vector3.down * 1.5f * Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }
}
