using UnityEngine;
using System.Collections;

/// <summary>
/// Bjorn en PRIMERA PERSONA:
/// - WASD: moverse    - Raton: mirar    - ESPACIO: saltar
/// - Clic izq: espadazo (ves tu espada en la mano)
/// - SHIFT: dash hacia donde te mueves
/// - Q: Rayo de Odin (se desbloquea con la Runa del Trueno)
/// La camara es hija del jugador, a la altura de los ojos, con balanceo al caminar.
/// </summary>
public class Jugador : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 6.5f;
    public float fuerzaSalto = 8f;
    public float sensibilidad = 2.6f;

    [Header("Combate")]
    public float vidaMax = 100f;
    [HideInInspector] public float vida;
    [HideInInspector] public bool controlActivo;
    [HideInInspector] public bool invulnerable;

    [Header("Poderes")]
    [HideInInspector] public bool poderRayo;
    public float cooldownDash = 1.2f;
    public float cooldownRayo = 3.5f;
    [HideInInspector] public float cdDashRestante;
    [HideInInspector] public float cdRayoRestante;

    private CharacterController control;
    private Transform camaraT;
    private Transform brazoPivote;   // el brazo con la espada frente a la camara
    private float pitch;
    private float velY;
    private float cdAtaque;
    private float faseBob;
    private bool enDash;
    private bool atacando;

    void Awake()
    {
        control = gameObject.AddComponent<CharacterController>();
        control.height = 1.8f;
        control.center = new Vector3(0f, 0.9f, 0f);
        control.radius = 0.35f;

        // La camara se vuelve los ojos de Bjorn
        camaraT = Camera.main.transform;
        camaraT.SetParent(transform, false);
        camaraT.localPosition = new Vector3(0f, 1.6f, 0f);
        camaraT.localRotation = Quaternion.identity;

        CrearBrazoConEspada();
        vida = vidaMax;
    }

    /// <summary>Modelo en primera persona: antebrazo + espada vikinga, abajo a la derecha.</summary>
    void CrearBrazoConEspada()
    {
        brazoPivote = new GameObject("BrazoEspada").transform;
        brazoPivote.SetParent(camaraT, false);
        brazoPivote.localPosition = new Vector3(0.42f, -0.38f, 0.55f);
        brazoPivote.localRotation = Quaternion.Euler(8f, -6f, 0f);

        Color piel  = new Color(0.95f, 0.78f, 0.62f);
        Color cuero = new Color(0.35f, 0.25f, 0.15f);
        Color metal = new Color(0.85f, 0.88f, 0.95f);

        Transform antebrazo = ConstructorPersonaje.Rect(brazoPivote, "Antebrazo", new Vector2(0f, -0.1f), new Vector2(0.09f, 0.09f), piel, 0, 0.34f);
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

        if (!controlActivo) return;

        // ---- Mirar con el raton ----
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

        bool enSuelo = control.isGrounded;
        if (enSuelo && velY < 0f) velY = -1.5f;
        if (Input.GetKeyDown(KeyCode.Space) && enSuelo) velY = fuerzaSalto;
        velY -= 20f * Time.deltaTime;

        Vector3 movimiento = enDash ? dirDash * 16f : dir * velocidad;
        control.Move((movimiento + Vector3.up * velY) * Time.deltaTime);

        // ---- Balanceo de la espada al caminar ----
        if (enSuelo && dir.sqrMagnitude > 0.01f && !atacando)
        {
            faseBob += Time.deltaTime * 9f;
            brazoPivote.localPosition = new Vector3(0.42f + Mathf.Sin(faseBob * 0.5f) * 0.015f,
                                                    -0.38f + Mathf.Abs(Mathf.Sin(faseBob)) * 0.03f, 0.55f);
        }

        // ---- Espadazo ----
        if (Input.GetMouseButtonDown(0) && cdAtaque <= 0f && Cursor.lockState == CursorLockMode.Locked)
        {
            cdAtaque = 0.5f;
            StartCoroutine(Atacar());
        }

        // ---- Dash ----
        if (Input.GetKeyDown(KeyCode.LeftShift) && cdDashRestante <= 0f && !enDash)
        {
            cdDashRestante = cooldownDash;
            StartCoroutine(Dash(dir.sqrMagnitude > 0.01f ? dir : transform.forward));
        }

        // ---- Rayo de Odin ----
        if (Input.GetKeyDown(KeyCode.Q) && poderRayo && cdRayoRestante <= 0f)
        {
            cdRayoRestante = cooldownRayo;
            Proyectil.Crear(camaraT.position + camaraT.forward * 0.8f, camaraT.forward);
            GestorAventura.Instancia.SonidoTrueno();
        }
    }

    private Vector3 dirDash;

    IEnumerator Atacar()
    {
        atacando = true;
        GestorAventura.Instancia.SonidoEspada();

        // animacion del tajo: atras -> golpe cruzado -> volver
        yield return GirarBrazo(new Vector3(8f, -6f, 0f), new Vector3(-35f, -40f, 10f), 0.08f);
        StartCoroutine(GolpeConecta());
        yield return GirarBrazo(new Vector3(-35f, -40f, 10f), new Vector3(35f, 30f, -15f), 0.09f);
        yield return GirarBrazo(new Vector3(35f, 30f, -15f), new Vector3(8f, -6f, 0f), 0.16f);
        atacando = false;
    }

    IEnumerator GolpeConecta()
    {
        yield return new WaitForSeconds(0.05f);
        foreach (Enemigo e in Enemigo.Todos.ToArray())
        {
            if (e == null || e.muerto) continue;
            Vector3 hacia = (e.transform.position + Vector3.up * 1.1f) - camaraT.position;
            if (hacia.magnitude < 3f && Vector3.Angle(camaraT.forward, hacia) < 42f)
                e.RecibirDano(34f, transform.position);
        }
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

    public void RecibirDano(float d)
    {
        if (!controlActivo || invulnerable) return;
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
