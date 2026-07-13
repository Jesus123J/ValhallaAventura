using UnityEngine;
using System.Collections;

/// <summary>
/// Bjorn Brazo-de-Hierro en 2D (vista lateral):
/// - A/D o flechas: correr    - ESPACIO: saltar
/// - Clic izq o J: espadazo   - SHIFT: dash (esquiva veloz)
/// - Q: Rayo de Odin (poder que se desbloquea al encontrar la Runa del Trueno)
/// </summary>
public class Jugador : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 6.2f;
    public float fuerzaSalto = 13.5f;

    [Header("Combate")]
    public float vidaMax = 100f;
    [HideInInspector] public float vida;
    [HideInInspector] public bool controlActivo;
    [HideInInspector] public bool invulnerable;

    [Header("Poderes")]
    [HideInInspector] public bool poderRayo;         // se desbloquea con la Runa del Trueno
    public float cooldownDash = 1.2f;
    public float cooldownRayo = 3.5f;
    [HideInInspector] public float cdDashRestante;
    [HideInInspector] public float cdRayoRestante;

    [HideInInspector] public int mirando = 1;        // 1 derecha, -1 izquierda

    private Rigidbody2D rb;
    private AnimadorPersonaje anim;
    private Transform visual;
    private float cdAtaque;
    private bool enDash;

    void Awake()
    {
        gameObject.layer = 2; // IgnoreRaycast: el chequeo de suelo no choca consigo mismo
        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3.4f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var col = gameObject.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(0.5f, 1.7f);
        col.offset = new Vector2(0f, 0.85f);

        anim = ConstructorPersonaje.CrearVisual(transform, false);
        visual = anim.transform;
        vida = vidaMax;
    }

    void Update()
    {
        cdAtaque -= Time.deltaTime;
        cdDashRestante -= Time.deltaTime;
        cdRayoRestante -= Time.deltaTime;

        if (!controlActivo)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
            anim.Caminando(false);
            return;
        }

        // ---- Correr ----
        float h = Input.GetAxisRaw("Horizontal");
        if (!enDash)
            rb.velocity = new Vector2(h * velocidad, rb.velocity.y);

        if (Mathf.Abs(h) > 0.01f)
        {
            mirando = h > 0f ? 1 : -1;
            visual.localScale = new Vector3(mirando, 1f, 1f);
        }
        anim.Caminando(Mathf.Abs(h) > 0.01f && EnSuelo());

        // ---- Saltar ----
        if (Input.GetKeyDown(KeyCode.Space) && EnSuelo())
            rb.velocity = new Vector2(rb.velocity.x, fuerzaSalto);

        // ---- Espadazo ----
        if ((Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J)) && cdAtaque <= 0f)
        {
            cdAtaque = 0.45f;
            StartCoroutine(Atacar());
        }

        // ---- Dash ----
        if (Input.GetKeyDown(KeyCode.LeftShift) && cdDashRestante <= 0f && !enDash)
        {
            cdDashRestante = cooldownDash;
            StartCoroutine(Dash());
        }

        // ---- Rayo de Odin ----
        if (Input.GetKeyDown(KeyCode.Q) && poderRayo && cdRayoRestante <= 0f)
        {
            cdRayoRestante = cooldownRayo;
            Proyectil.Crear(transform.position + new Vector3(mirando * 0.7f, 1.35f, 0f), mirando);
            GestorAventura.Instancia.SonidoTrueno();
        }
    }

    bool EnSuelo()
    {
        return Physics2D.Raycast(transform.position + Vector3.up * 0.1f, Vector2.down, 0.28f);
    }

    IEnumerator Atacar()
    {
        anim.Atacar();
        GestorAventura.Instancia.SonidoEspada();
        yield return new WaitForSeconds(0.12f); // el tajo conecta a mitad del golpe

        foreach (Enemigo e in Enemigo.Todos.ToArray())
        {
            if (e == null || e.muerto) continue;
            float dx = e.transform.position.x - transform.position.x;
            float dy = Mathf.Abs(e.transform.position.y - transform.position.y);
            bool delante = Mathf.Sign(dx) == mirando || Mathf.Abs(dx) < 0.4f;
            if (delante && Mathf.Abs(dx) < 2.0f && dy < 1.6f)
                e.RecibirDano(34f, transform.position);
        }
    }

    IEnumerator Dash()
    {
        enDash = true;
        invulnerable = true;
        float gravedad = rb.gravityScale;
        rb.gravityScale = 0f;
        rb.velocity = new Vector2(mirando * 17f, 0f);

        // estela fantasma del dash
        for (int i = 0; i < 3; i++)
        {
            var estela = ConstructorPersonaje.Rect(null, "Estela",
                (Vector2)transform.position + new Vector2(0f, 1.1f),
                new Vector2(0.6f, 1.6f), new Color(0.5f, 0.8f, 1f, 0.35f), 0);
            Destroy(estela.gameObject, 0.25f);
            yield return new WaitForSeconds(0.05f);
        }

        rb.gravityScale = gravedad;
        enDash = false;
        invulnerable = false;
    }

    public void RecibirDano(float d)
    {
        if (!controlActivo || invulnerable) return;
        vida -= d;
        GestorAventura.Instancia.JugadorHerido();
        StartCoroutine(ParpadeoDano());
        if (vida <= 0f)
        {
            vida = 0f;
            GestorAventura.Instancia.JugadorCaido();
        }
    }

    IEnumerator ParpadeoDano()
    {
        invulnerable = true;
        for (int i = 0; i < 3; i++)
        {
            visual.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.06f);
            visual.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.08f);
        }
        invulnerable = false;
    }

    public void Teletransportar(Vector3 pos)
    {
        rb.velocity = Vector2.zero;
        transform.position = pos;
        vida = vidaMax;
    }
}
