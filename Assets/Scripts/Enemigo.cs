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

    /// <summary>Dificultad adaptativa invisible: el gestor lo baja si el jugador muere seguido.</summary>
    public static float FactorDanoGlobal = 1f;

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

    // comportamiento dinamico: rodear como lobo y embestir
    private int dirOrbita = 1;
    private float proxCambioOrbita;
    private float proxEmbestida;
    private bool embistiendo;

    void Awake()
    {
        anim = ConstructorPersonaje.CrearVisual(transform, true);
        // senal de ataque: rombo rojo brillante sobre la cabeza (oculto)
        senal = ConstructorPersonaje.Rect(anim.transform, "Senal", new Vector2(0f, 2.6f),
                                          new Vector2(0.3f, 0.3f), new Color(1f, 0.25f, 0.1f), 7, 0.1f, true);
        senal.localRotation = Quaternion.Euler(0f, 0f, 45f);
        senal.gameObject.SetActive(false);
        dirOrbita = Random.value < 0.5f ? -1 : 1;
        proxEmbestida = Time.time + Random.Range(4f, 9f);
        Todos.Add(this);
    }

    void OnDestroy() { Todos.Remove(this); }

    void LateUpdate()
    {
        // pisar siempre el terreno voxel (excepto al hundirse muerto)
        if (muerto) return;
        Vector3 p = transform.position;
        p.y = GeneradorMundo.Altura(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
        transform.position = p;
    }

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
        if (muerto || jugador == null || !jugador.controlActivo || Time.time < aturdidoHasta || embistiendo)
        {
            if (!muerto && anim != null && !embistiendo) anim.Caminando(false);
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
            Vector3 avance = hacia.normalized;

            // de cerca RODEA como lobo en vez de venir en linea recta
            if (dist < 4.5f && !esJefe)
            {
                if (Time.time > proxCambioOrbita)
                {
                    dirOrbita = -dirOrbita;
                    proxCambioOrbita = Time.time + Random.Range(2f, 4f);
                }
                Vector3 tangente = Vector3.Cross(Vector3.up, avance) * dirOrbita;
                avance = (avance * 0.35f + tangente * 0.65f).normalized;
            }

            transform.position += avance * velocidad * Time.deltaTime;
            anim.Caminando(true);

            // EMBESTIDA sorpresa desde media distancia (telegrafiada)
            if (!esJefe && dist > 5f && dist < 12f && Time.time > proxEmbestida)
                StartCoroutine(Embestida());
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

    /// <summary>Se agazapa (aviso) y se lanza a la carrera contra el jugador.</summary>
    IEnumerator Embestida()
    {
        embistiendo = true;
        proxEmbestida = Time.time + Random.Range(5f, 10f);

        // aviso: se agazapa y enciende la senal
        senal.gameObject.SetActive(true);
        Vector3 esc = transform.localScale;
        transform.localScale = new Vector3(esc.x, esc.y * 0.82f, esc.z);
        yield return new WaitForSeconds(0.35f);
        transform.localScale = esc;
        senal.gameObject.SetActive(false);
        if (muerto || jugador == null) { embistiendo = false; yield break; }

        float fin = Time.time + 0.7f;
        while (Time.time < fin && !muerto && jugador != null)
        {
            Vector3 h = jugador.transform.position - transform.position;
            h.y = 0f;
            if (h.magnitude < 1.7f)
            {
                anim.Atacar();
                yield return new WaitForSeconds(0.15f);
                if (!muerto && h.magnitude < 2.3f)
                    jugador.RecibirDanoDesde(dano * FactorDanoGlobal, this);
                break;
            }
            transform.position += h.normalized * velocidad * 3.2f * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(h);
            anim.Caminando(true);
            yield return null;
        }
        embistiendo = false;
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
            jugador.RecibirDanoDesde(dano * FactorDanoGlobal, this);
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
