using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Draugr 3D con combate LEGIBLE: antes de golpear enciende una senal roja
/// sobre la cabeza y alza el arma casi medio segundo - siempre puedes
/// esquivar, bloquear o hacer parry. Un parry lo deja ATURDIDO.
/// El jefe guardian es mas grande, resistente y fuerte.
/// </summary>
public class Enemigo : MonoBehaviour
{
    public static readonly List<Enemigo> Todos = new List<Enemigo>();

    [HideInInspector] public float vida = 100f;
    [HideInInspector] public bool muerto;
    [HideInInspector] public bool esJefe;

    private float velocidad = 3f;
    private float dano = 8f;
    private float alcance = 1.9f;
    private Jugador jugador;
    private AnimadorPersonaje anim;
    private Transform senal;          // aviso visual del ataque
    private float cooldown;
    private float aturdidoHasta;

    void Awake()
    {
        anim = ConstructorPersonaje.CrearVisual(transform, true);
        // senal de ataque: rombo rojo brillante sobre la cabeza (oculto)
        senal = ConstructorPersonaje.Rect(anim.transform, "Senal", new Vector2(0f, 2.6f),
                                          new Vector2(0.3f, 0.3f), new Color(1f, 0.25f, 0.1f), 7, 0.1f, true);
        senal.localRotation = Quaternion.Euler(0f, 0f, 45f);
        senal.gameObject.SetActive(false);
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
            dano = 15f;
            velocidad = 2.5f;
            alcance = 2.6f;
            ConstructorPersonaje.Rect(anim.transform, "Corona", new Vector2(0.02f, 2.2f),
                                      new Vector2(0.5f, 0.14f), new Color(0.95f, 0.75f, 0.15f), 7, 0.44f, true);
        }
    }

    public void Aturdir(float segundos)
    {
        aturdidoHasta = Time.time + segundos;
        StartCoroutine(MostrarAturdido(segundos));
    }

    IEnumerator MostrarAturdido(float segundos)
    {
        senal.gameObject.SetActive(true);
        var mr = senal.GetComponent<MeshRenderer>();
        Material original = mr.sharedMaterial;
        mr.sharedMaterial = ConstructorPersonaje.MatEmisivo(Color.white);
        float fin = Time.time + segundos;
        while (Time.time < fin && !muerto)
        {
            senal.localRotation = Quaternion.Euler(0f, Time.time * 400f, 45f); // gira: mareado
            yield return null;
        }
        mr.sharedMaterial = original;
        senal.gameObject.SetActive(false);
    }

    void Update()
    {
        if (muerto || jugador == null || !jugador.controlActivo || Time.time < aturdidoHasta)
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
                cooldown = esJefe ? 1.8f : 1.4f;
                StartCoroutine(Atacar());
            }
        }
    }

    IEnumerator Atacar()
    {
        // TELEGRAFIA: senal roja + arma en alto durante 0.45s -> el golpe SIEMPRE se ve venir
        senal.gameObject.SetActive(true);
        anim.Atacar();
        yield return new WaitForSeconds(0.45f);
        senal.gameObject.SetActive(false);
        if (muerto || jugador == null || Time.time < aturdidoHasta) yield break;

        Vector3 hacia = jugador.transform.position - transform.position;
        hacia.y = 0f;
        if (hacia.magnitude < alcance + 0.7f)
            jugador.RecibirDanoDesde(dano, this);
    }

    public void RecibirDano(float d, Vector3 origen, float empuje = 0.55f, bool critico = false)
    {
        if (muerto) return;
        vida -= d;
        GestorAventura.Instancia.SonidoImpacto();
        DanoFlotante.Crear(transform.position + Vector3.up * (esJefe ? 2.6f : 2.1f), d, critico);

        Vector3 dir = transform.position - origen;
        dir.y = 0f;
        transform.position += dir.normalized * empuje * (esJefe ? 0.45f : 1f);
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
        senal.gameObject.SetActive(false);
        GestorAventura.Instancia.EnemigoMuerto(this);

        Quaternion inicio = transform.rotation;
        Quaternion caido = inicio * Quaternion.Euler(-88f, 0f, 0f);
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
