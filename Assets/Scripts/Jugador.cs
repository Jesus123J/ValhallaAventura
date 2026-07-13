using UnityEngine;
using System.Collections;

/// <summary>
/// Bjorn en primera persona - kit de combate completo:
/// - WASD + raton. ESPACIO: salto DOBLE con coyote time (perdona errores).
/// - Clic corto: COMBO de 3 espadazos (el tercero es un remate brutal).
/// - Mantener clic: GOLPE CARGADO giratorio de area (360 grados).
/// - Clic derecho: BLOQUEO (reduce 80% el dano). Si bloqueas justo a tiempo: PARRY (aturde al enemigo).
/// - SHIFT: dash con invulnerabilidad.   Q: Rayo de Odin (3 niveles de poder).
/// - La camara hace zoom sutil cuando hay enemigos cerca (tension de combate).
/// </summary>
public class Jugador : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 6.5f;
    public float fuerzaSalto = 8f;
    public float sensibilidad = 2.6f;

    [Header("Combate (la tienda mejora estos valores)")]
    public float vidaMax = 100f;
    public float danoEspada = 34f;
    public float danoMultiplicador = 1f;
    public float reduccionDano = 0f;      // Piel de Hierro
    public float tiempoCarga = 0.6f;      // Golpe cargado (habilidad lo acelera)
    public float bonoRemate = 1.7f;       // Remate Brutal lo sube
    public int nivelRayo = 1;             // 1: rayo  2: atraviesa  3: trueno en area
    [HideInInspector] public float vida;
    [HideInInspector] public bool controlActivo;
    [HideInInspector] public bool invulnerable;
    [HideInInspector] public bool poderRayo;

    [Header("Cooldowns")]
    public float cooldownDash = 1.2f;
    public float cooldownRayo = 3.5f;
    [HideInInspector] public float cdDashRestante;
    [HideInInspector] public float cdRayoRestante;

    // estado visible para el HUD
    [HideInInspector] public float carga01;
    [HideInInspector] public int combo;
    [HideInInspector] public float comboHasta;
    [HideInInspector] public bool bloqueando;

    private CharacterController control;
    private Transform camaraT;
    private Camera camara;
    private Transform brazoPivote;
    private float pitch;
    private float velY;
    private float cdAtaque;
    private float faseBob;
    private bool enDash;
    private bool atacando;
    private Vector3 dirDash;

    // salto amable
    private float ultimoSuelo = -99f;
    private float saltoPedido = -99f;
    private int saltosUsados;

    // golpe cargado y bloqueo
    private float inicioPresion = -99f;
    private bool cargando;
    private float inicioBloqueo = -99f;
    private float ultimoGolpe = -99f;

    static readonly Vector3 BrazoReposo = new Vector3(8f, -6f, 0f);

    void Awake()
    {
        control = gameObject.AddComponent<CharacterController>();
        control.height = 1.8f;
        control.center = new Vector3(0f, 0.9f, 0f);
        control.radius = 0.35f;

        camara = Camera.main;
        camaraT = camara.transform;
        camaraT.SetParent(transform, false);
        camaraT.localPosition = new Vector3(0f, 1.6f, 0f);
        camaraT.localRotation = Quaternion.identity;

        CrearBrazoConEspada();
        vida = vidaMax;
    }

    void CrearBrazoConEspada()
    {
        brazoPivote = new GameObject("BrazoEspada").transform;
        brazoPivote.SetParent(camaraT, false);
        brazoPivote.localPosition = new Vector3(0.42f, -0.38f, 0.55f);
        brazoPivote.localRotation = Quaternion.Euler(BrazoReposo);

        Color piel  = new Color(0.95f, 0.78f, 0.62f);
        Color cuero = new Color(0.35f, 0.25f, 0.15f);
        Color metal = new Color(0.85f, 0.88f, 0.95f);

        Transform antebrazo = ConstructorPersonaje.Rect(brazoPivote, "Antebrazo", Vector2.zero, new Vector2(0.09f, 0.09f), piel, 0, 0.34f);
        antebrazo.localPosition = new Vector3(0f, -0.10f, -0.05f);
        Transform guante = ConstructorPersonaje.Rect(brazoPivote, "Guante", Vector2.zero, new Vector2(0.11f, 0.11f), cuero, 0, 0.12f);
        guante.localPosition = new Vector3(0f, -0.06f, 0.13f);
        Transform mango = ConstructorPersonaje.Rect(brazoPivote, "Mango", Vector2.zero, new Vector2(0.05f, 0.05f), cuero, 0, 0.26f);
        mango.localPosition = new Vector3(0f, 0f, 0.16f);
        Transform guarda = ConstructorPersonaje.Rect(brazoPivote, "Guarda", Vector2.zero, new Vector2(0.24f, 0.05f), new Color(0.9f, 0.75f, 0.2f), 0, 0.05f);
        guarda.localPosition = new Vector3(0f, 0f, 0.30f);
        Transform hoja = ConstructorPersonaje.Rect(brazoPivote, "Hoja", Vector2.zero, new Vector2(0.10f, 0.035f), metal, 0, 0.85f);
        hoja.localPosition = new Vector3(0f, 0f, 0.74f);
        Transform filo = ConstructorPersonaje.Rect(brazoPivote, "Filo", Vector2.zero, new Vector2(0.03f, 0.04f), new Color(1f, 1f, 1f, 0.9f), 0, 0.8f, true);
        filo.localPosition = new Vector3(0f, 0.03f, 0.74f);
    }

    void Update()
    {
        cdAtaque -= Time.deltaTime;
        cdDashRestante -= Time.deltaTime;
        cdRayoRestante -= Time.deltaTime;

        ActualizarFOV();

        if (!controlActivo) return;

        // ---- Mirar ----
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            transform.Rotate(0f, Input.GetAxis("Mouse X") * sensibilidad, 0f);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensibilidad, -70f, 70f);
            camaraT.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // ---- Moverse ----
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = (transform.forward * v + transform.right * h);
        if (dir.magnitude > 1f) dir.Normalize();
        float velActual = bloqueando ? velocidad * 0.4f : velocidad;

        // ---- Salto amable: coyote time + doble salto + buffer ----
        bool enSuelo = control.isGrounded;
        if (enSuelo)
        {
            ultimoSuelo = Time.time;
            saltosUsados = 0;
            if (velY < 0f) velY = -1.5f;
        }
        if (Input.GetKeyDown(KeyCode.Space)) saltoPedido = Time.time;

        bool quiereSaltar = Time.time - saltoPedido < 0.12f; // buffer: el salto "se guarda"
        if (quiereSaltar)
        {
            bool coyote = Time.time - ultimoSuelo < 0.15f;   // acaba de dejar el borde: aun vale
            if (coyote && saltosUsados == 0)
            {
                velY = fuerzaSalto; saltosUsados = 1; saltoPedido = -99f;
            }
            else if (saltosUsados >= 1 && saltosUsados < 2)
            {
                velY = fuerzaSalto * 0.95f; saltosUsados = 2; saltoPedido = -99f; // doble salto
            }
            else if (saltosUsados == 0 && !coyote)
            {
                velY = fuerzaSalto * 0.95f; saltosUsados = 2; saltoPedido = -99f; // cayo sin saltar: un salvavidas
            }
        }
        velY -= 20f * Time.deltaTime;

        Vector3 movimiento = enDash ? dirDash * 16f : dir * velActual;
        control.Move((movimiento + Vector3.up * velY) * Time.deltaTime);

        // ---- Balanceo al caminar ----
        if (enSuelo && dir.sqrMagnitude > 0.01f && !atacando && !cargando && !bloqueando)
        {
            faseBob += Time.deltaTime * 9f;
            brazoPivote.localPosition = new Vector3(0.42f + Mathf.Sin(faseBob * 0.5f) * 0.015f,
                                                    -0.38f + Mathf.Abs(Mathf.Sin(faseBob)) * 0.03f, 0.55f);
        }

        ManejarAtaques();
        ManejarBloqueo();

        // ---- Rayo de Odin ----
        if (Input.GetKeyDown(KeyCode.Q) && poderRayo && cdRayoRestante <= 0f)
        {
            cdRayoRestante = cooldownRayo;
            Proyectil.Crear(camaraT.position + camaraT.forward * 0.8f, camaraT.forward, nivelRayo);
            GestorAventura.Instancia.SonidoTrueno();
        }

        // ---- Dash ----
        if (Input.GetKeyDown(KeyCode.LeftShift) && cdDashRestante <= 0f && !enDash)
        {
            cdDashRestante = cooldownDash;
            StartCoroutine(Dash(dir.sqrMagnitude > 0.01f ? dir : transform.forward));
        }
    }

    // ---- Zoom de combate: FOV segun tension ----
    void ActualizarFOV()
    {
        float objetivo = 70f;
        if (enDash) objetivo = 79f;
        else
        {
            foreach (Enemigo e in Enemigo.Todos)
            {
                if (e == null || e.muerto) continue;
                if ((e.transform.position - transform.position).sqrMagnitude < 64f) // 8m
                {
                    objetivo = 65f; // acercamiento sutil: tension
                    break;
                }
            }
        }
        camara.fieldOfView = Mathf.Lerp(camara.fieldOfView, objetivo, 5f * Time.deltaTime);
    }

    // ---- Clic corto = combo | mantener = golpe cargado ----
    void ManejarAtaques()
    {
        if (Cursor.lockState != CursorLockMode.Locked || bloqueando) { cargando = false; carga01 = 0f; return; }

        if (Input.GetMouseButtonDown(0) && cdAtaque <= 0f && !atacando)
        {
            inicioPresion = Time.time;
            cargando = true;
        }

        if (cargando)
        {
            float sostenido = Time.time - inicioPresion;
            carga01 = Mathf.Clamp01((sostenido - 0.18f) / (tiempoCarga - 0.18f));

            // el brazo se echa atras mientras cargas
            if (carga01 > 0f && !atacando)
                brazoPivote.localRotation = Quaternion.Euler(Vector3.Lerp(BrazoReposo, new Vector3(-55f, -55f, 15f), carga01));

            if (Input.GetMouseButtonUp(0))
            {
                cargando = false;
                carga01 = 0f;
                cdAtaque = 0.35f;
                if (sostenido >= tiempoCarga)
                    StartCoroutine(GolpeCargado());
                else
                {
                    combo = (Time.time - ultimoGolpe < 1.0f) ? combo + 1 : 1;
                    if (combo > 3) combo = 1;
                    ultimoGolpe = Time.time;
                    comboHasta = Time.time + 1.0f;
                    StartCoroutine(Atacar(combo));
                }
            }
        }
    }

    void ManejarBloqueo()
    {
        if (Input.GetMouseButtonDown(1) && !atacando && !cargando)
        {
            bloqueando = true;
            inicioBloqueo = Time.time;
            brazoPivote.localRotation = Quaternion.Euler(0f, -70f, 80f); // espada cruzada como guardia
            brazoPivote.localPosition = new Vector3(0.25f, -0.28f, 0.5f);
        }
        if (bloqueando && (Input.GetMouseButtonUp(1) || atacando))
        {
            bloqueando = false;
            brazoPivote.localRotation = Quaternion.Euler(BrazoReposo);
            brazoPivote.localPosition = new Vector3(0.42f, -0.38f, 0.55f);
        }
    }

    IEnumerator Atacar(int golpe)
    {
        atacando = true;
        GestorAventura.Instancia.SonidoEspada();

        Vector3 atras, fin;
        switch (golpe)
        {
            case 2:  // reves: de izquierda a derecha
                atras = new Vector3(-30f, 45f, -12f); fin = new Vector3(30f, -50f, 12f); break;
            case 3:  // remate: de arriba hacia abajo
                atras = new Vector3(-80f, 0f, 0f); fin = new Vector3(50f, 0f, 0f); break;
            default: // tajo cruzado
                atras = new Vector3(-35f, -40f, 10f); fin = new Vector3(35f, 30f, -15f); break;
        }

        yield return GirarBrazo(BrazoReposo, atras, golpe == 3 ? 0.11f : 0.08f);
        StartCoroutine(GolpeConecta(golpe));
        yield return GirarBrazo(atras, fin, 0.09f);
        yield return GirarBrazo(fin, BrazoReposo, 0.15f);
        atacando = false;
    }

    IEnumerator GolpeConecta(int golpe)
    {
        yield return new WaitForSeconds(0.05f);
        bool esRemate = golpe == 3;
        if (esRemate) GestorAventura.Instancia.SonidoRemate();

        float alcance = esRemate ? 3.5f : 3f;
        float angulo = esRemate ? 55f : 42f;
        foreach (Enemigo e in Enemigo.Todos.ToArray())
        {
            if (e == null || e.muerto) continue;
            Vector3 hacia = (e.transform.position + Vector3.up * 1.1f) - camaraT.position;
            if (hacia.magnitude < alcance && Vector3.Angle(camaraT.forward, hacia) < angulo)
            {
                bool critico = Random.value < 0.10f;
                float dano = danoEspada * danoMultiplicador * (esRemate ? bonoRemate : 1f) * (critico ? 1.5f : 1f);
                e.RecibirDano(dano, transform.position, esRemate ? 1.2f : 0.55f, critico);
            }
        }
    }

    IEnumerator GolpeCargado()
    {
        atacando = true;
        GestorAventura.Instancia.SonidoRemate();
        GestorAventura.Instancia.SonidoEspada();

        // barrido giratorio de 360 grados
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            brazoPivote.localRotation = Quaternion.Euler(10f, -55f + (t / 0.35f) * 400f, 20f);
            yield return null;
        }
        brazoPivote.localRotation = Quaternion.Euler(BrazoReposo);

        foreach (Enemigo e in Enemigo.Todos.ToArray())
        {
            if (e == null || e.muerto) continue;
            Vector3 hacia = e.transform.position - transform.position;
            hacia.y = 0f;
            if (hacia.magnitude < 3.6f) // en TODAS las direcciones
            {
                bool critico = Random.value < 0.10f;
                float dano = danoEspada * danoMultiplicador * 1.5f * (critico ? 1.5f : 1f);
                e.RecibirDano(dano, transform.position, 1.4f, critico);
            }
        }
        atacando = false;
    }

    IEnumerator GirarBrazo(Vector3 de, Vector3 a, float dur)
    {
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            brazoPivote.localRotation = Quaternion.Euler(Vector3.Lerp(de, a, t / dur));
            yield return null;
        }
        brazoPivote.localRotation = Quaternion.Euler(a);
    }

    IEnumerator Dash(Vector3 dir)
    {
        enDash = true;
        invulnerable = true;
        dirDash = dir.normalized;
        yield return new WaitForSeconds(0.16f);
        enDash = false;
        invulnerable = false;
    }

    /// <summary>Recibe dano de un enemigo. Bloqueo: -80%. Parry (bloqueo reciente): 0 y aturde al atacante.</summary>
    public void RecibirDanoDesde(float d, Enemigo atacante)
    {
        if (!controlActivo || invulnerable) return;

        if (bloqueando)
        {
            if (Time.time - inicioBloqueo <= 0.28f) // ¡PARRY!
            {
                GestorAventura.Instancia.SonidoParry();
                if (atacante != null) atacante.Aturdir(1.6f);
                GestorAventura.Instancia.MostrarAviso("¡PARRY!");
                return;
            }
            d *= 0.2f; // bloqueo normal
            GestorAventura.Instancia.SonidoImpacto();
        }

        d *= (1f - reduccionDano);
        vida -= d;
        GestorAventura.Instancia.JugadorHerido();
        StartCoroutine(SacudidaCamara());
        if (vida <= 0f)
        {
            vida = 0f;
            GestorAventura.Instancia.JugadorCaido();
        }
    }

    IEnumerator SacudidaCamara()
    {
        Vector3 basePos = new Vector3(0f, 1.6f, 0f);
        for (int i = 0; i < 5; i++)
        {
            camaraT.localPosition = basePos + (Vector3)Random.insideUnitCircle * 0.06f;
            yield return null;
            yield return null;
        }
        camaraT.localPosition = basePos;
    }

    public void Teletransportar(Vector3 pos)
    {
        control.enabled = false;
        transform.position = pos;
        control.enabled = true;
        velY = 0f;
        vida = vidaMax;
    }
}
