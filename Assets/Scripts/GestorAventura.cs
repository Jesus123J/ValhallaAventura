using UnityEngine;
using System.Collections;

/// <summary>
/// Director del juego 2.5D "Camino al Valhalla - Capitulo 1":
/// - Construye TODO el nivel con geometria 3D real (luz, sombra, niebla) por codigo:
///   aurora boreal y montanas a profundidad real (parallax autentico de perspectiva),
///   suelo nevado, plataformas, pilares runicos brillantes, premios, draugr y jefe.
/// - El gameplay sigue siendo lateral y simple (fisica 2D intacta).
/// - Modo historia narrado, premios, poderes, HUD y guardado automatico.
/// </summary>
public class GestorAventura : MonoBehaviour
{
    public static GestorAventura Instancia;
    public Jugador Bjorn { get; private set; }

    static readonly Vector3 InicioJugador = new Vector3(-6f, 1.5f, 0f);
    int metaEnemigos;

    Transform portal;
    MeshRenderer puertaMR;
    Light luzPortal;
    bool portalActivo;

    int almas;
    int muertos;

    enum Estado { Intro, Jugando, Fin }
    Estado estado = Estado.Intro;
    string mensajeCentral = "";
    float avisoGuardadoHasta;

    AudioSource musicaSrc, sfxSrc, vozSrc;
    AudioClip sfxEspada, sfxImpacto, sfxHerido, sfxFanfarria, sfxAlmas, sfxTrueno;
    Texture2D texBlanca;
    GUIStyle estiloCentro, estiloHud;
    Camara2D camara;

    void Awake() { Instancia = this; }

    void Start()
    {
        musicaSrc = gameObject.AddComponent<AudioSource>();
        musicaSrc.clip = GeneradorAudio.MusicaNordica();
        musicaSrc.loop = true;
        musicaSrc.volume = 0.3f;
        musicaSrc.Play();
        sfxSrc = gameObject.AddComponent<AudioSource>();
        vozSrc = gameObject.AddComponent<AudioSource>();
        sfxEspada = GeneradorAudio.Espada();
        sfxImpacto = GeneradorAudio.Impacto();
        sfxHerido = GeneradorAudio.Herido();
        sfxFanfarria = GeneradorAudio.Fanfarria();
        sfxAlmas = GeneradorAudio.Almas();
        sfxTrueno = GeneradorAudio.Trueno();

        texBlanca = new Texture2D(1, 1);
        texBlanca.SetPixel(0, 0, Color.white);
        texBlanca.Apply();

        ConstruirNivel();
        almas = PlayerPrefs.GetInt("almas", 0);
        StartCoroutine(Intro());
    }

    // ============================================================
    //  CONSTRUCCION DEL NIVEL 2.5D
    // ============================================================
    void ConstruirNivel()
    {
        // ---- Camara en perspectiva + ambiente ----
        Camera cam = Camera.main;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.19f); // noche polar profunda
        camara = cam.gameObject.AddComponent<Camara2D>();

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.10f, 0.12f, 0.25f);
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.ambientLight = new Color(0.34f, 0.37f, 0.50f);

        Light sol = FindObjectOfType<Light>();
        if (sol != null)
        {
            sol.type = LightType.Directional;
            sol.color = new Color(0.82f, 0.86f, 1f);
            sol.intensity = 1.05f;
            sol.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            sol.shadows = LightShadows.Soft;
        }

        // ---- Jugador ----
        GameObject bjornGO = new GameObject("Bjorn");
        bjornGO.transform.position = InicioJugador;
        Bjorn = bjornGO.AddComponent<Jugador>();
        camara.objetivo = bjornGO.transform;

        // ---- Fondo lejano: aurora boreal (profundidad real) ----
        Color[] coloresAurora =
        {
            new Color(0.15f, 0.9f, 0.55f, 0.35f),
            new Color(0.3f, 0.75f, 1f, 0.30f),
            new Color(0.7f, 0.4f, 1f, 0.28f)
        };
        for (int i = 0; i < 3; i++)
        {
            Transform banda = ConstructorPersonaje.Rect(null, "Aurora_" + i,
                Vector2.zero, new Vector2(320f, 5f), coloresAurora[i], 0, 0.5f, true);
            banda.position = new Vector3(55f, 26f + i * 5f, 70f);
            banda.rotation = Quaternion.Euler(0f, 0f, -3f + i * 3f);
        }

        // ---- Luna brillante con halo ----
        Transform halo = ConstructorPersonaje.Circ(null, "HaloLuna", Vector2.zero, 11f, new Color(0.9f, 0.95f, 1f, 0.18f), 0, true);
        halo.position = new Vector3(25f, 26f, 60f);
        Transform luna = ConstructorPersonaje.Circ(null, "Luna", Vector2.zero, 7f, new Color(0.95f, 0.97f, 1f), 0, true);
        luna.position = new Vector3(25f, 26f, 60f);

        // ---- Montanas: dos cordilleras a distinta profundidad ----
        for (int i = 0; i < 8; i++)
        {
            Transform m = ConstructorPersonaje.Rect(null, "MontanaLejos_" + i,
                Vector2.zero, new Vector2(34f, 34f), new Color(0.15f, 0.19f, 0.37f), 0, 4f);
            m.position = new Vector3(-15f + i * 26f, -2f, 42f);
            m.rotation = Quaternion.Euler(0f, 0f, 45f);
        }
        for (int i = 0; i < 10; i++)
        {
            Transform m = ConstructorPersonaje.Rect(null, "MontanaCerca_" + i,
                Vector2.zero, new Vector2(18f, 18f), new Color(0.23f, 0.29f, 0.51f), 0, 3f);
            m.position = new Vector3(-16f + i * 17f, -3f, 22f);
            m.rotation = Quaternion.Euler(0f, 0f, 45f);
            ConstructorPersonaje.Rect(m, "Nieve", new Vector2(0.30f, 0.30f), new Vector2(0.42f, 0.42f), new Color(0.75f, 0.82f, 0.95f), 1, 1.05f);
        }

        // ---- Suelo 3D: tierra con manto de nieve (fisica 2D intacta) ----
        Transform suelo = ConstructorPersonaje.Rect(null, "Suelo", new Vector2(57f, -1.6f), new Vector2(160f, 3.2f), new Color(0.30f, 0.24f, 0.30f), 0, 8f);
        suelo.gameObject.AddComponent<BoxCollider2D>();
        ConstructorPersonaje.Rect(null, "NieveSuelo", new Vector2(57f, -0.20f), new Vector2(160f, 0.5f), new Color(0.87f, 0.91f, 0.98f), 0, 7.8f);

        // ---- Muros invisibles ----
        Transform muroIzq = ConstructorPersonaje.Rect(null, "MuroIzq", new Vector2(-13.5f, 4f), new Vector2(1f, 14f), new Color(0f, 0f, 0f, 0f), 0);
        muroIzq.gameObject.AddComponent<BoxCollider2D>();
        Transform muroDer = ConstructorPersonaje.Rect(null, "MuroDer", new Vector2(126f, 4f), new Vector2(1f, 14f), new Color(0f, 0f, 0f, 0f), 0);
        muroDer.gameObject.AddComponent<BoxCollider2D>();

        // ---- Plataformas flotantes de piedra ----
        float[,] plataformas = { {18f, 2.2f}, {26f, 3.4f}, {40f, 2.5f}, {55f, 3.3f}, {68f, 2.3f}, {90f, 3f} };
        for (int i = 0; i < plataformas.GetLength(0); i++)
        {
            Transform p = ConstructorPersonaje.Rect(null, "Plataforma_" + i,
                new Vector2(plataformas[i, 0], plataformas[i, 1]), new Vector2(3.6f, 0.5f), new Color(0.42f, 0.46f, 0.58f), 0, 2.4f);
            p.gameObject.AddComponent<BoxCollider2D>();
            ConstructorPersonaje.Rect(p, "NievePlat", new Vector2(0f, 0.42f), new Vector2(1f, 0.28f), new Color(0.87f, 0.91f, 0.98f), 0, 0.95f);
        }

        // ---- Pilares runicos con runas que brillan ----
        float[] pilares = { 5f, 24f, 47f, 65f, 82f, 105f };
        Color[] coloresRuna = { new Color(0.3f, 0.9f, 1f), new Color(0.4f, 1f, 0.6f), new Color(0.8f, 0.5f, 1f) };
        for (int i = 0; i < pilares.Length; i++)
        {
            Transform pilar = ConstructorPersonaje.Rect(null, "Pilar_" + i, Vector2.zero, new Vector2(0.9f, 3.6f), new Color(0.33f, 0.36f, 0.46f), 0, 0.9f);
            pilar.position = new Vector3(pilares[i], 1.6f, 2.5f); // un paso detras del carril de juego
            Color runa = coloresRuna[i % coloresRuna.Length];
            ConstructorPersonaje.Rect(pilar, "Runa1", new Vector2(0f, 0.22f), new Vector2(0.30f, 0.10f), runa, 1, 0.15f, true);
            ConstructorPersonaje.Rect(pilar, "Runa2", new Vector2(0f, 0.05f), new Vector2(0.10f, 0.28f), runa, 1, 0.15f, true);
        }

        // ---- Arboles nevados (detras del carril) ----
        float[] arboles = { 11f, 33f, 59f, 76f, 96f };
        foreach (float x in arboles)
        {
            Transform tronco = ConstructorPersonaje.Rect(null, "Arbol", Vector2.zero, new Vector2(0.35f, 2.2f), new Color(0.35f, 0.25f, 0.2f), 0, 0.4f);
            tronco.position = new Vector3(x, 1.1f, 4f);
            for (int j = 0; j < 3; j++)
            {
                Transform copa = ConstructorPersonaje.Rect(tronco, "Copa_" + j, new Vector2(0f, 0.55f + j * 0.35f), new Vector2(4.6f - j * 1.2f, 1.6f), new Color(0.16f, 0.38f, 0.30f), 0, 3f - j * 0.7f);
                copa.localRotation = Quaternion.Euler(0f, 0f, 45f);
                ConstructorPersonaje.Rect(copa, "NieveCopa", new Vector2(0.3f, 0.3f), new Vector2(0.4f, 0.4f), new Color(0.87f, 0.91f, 0.98f), 1, 0.9f);
            }
        }

        // ---- PREMIOS ----
        float[,] posAlmas = { {10,0.7f}, {14,0.7f}, {18,3.2f}, {26,4.4f}, {33,0.7f}, {40,3.5f}, {47,0.7f}, {52,0.7f},
                              {63,0.7f}, {68,3.3f}, {76,0.7f}, {90,4f}, {95,0.7f}, {106,0.7f} };
        for (int i = 0; i < posAlmas.GetLength(0); i++)
            Recompensa.Crear(Recompensa.Tipo.Almas, new Vector2(posAlmas[i, 0], posAlmas[i, 1]));

        Recompensa.Crear(Recompensa.Tipo.Pocion, new Vector2(35f, 0.8f));
        Recompensa.Crear(Recompensa.Tipo.Pocion, new Vector2(80f, 0.8f));
        Recompensa.Crear(Recompensa.Tipo.RunaTrueno, new Vector2(55f, 4.3f));

        // ---- ENEMIGOS: 6 draugr + jefe guardian ----
        float[] draugr = { 20f, 30f, 42f, 60f, 72f, 85f };
        foreach (float x in draugr)
        {
            GameObject e = new GameObject("Draugr");
            e.transform.position = new Vector3(x, 0f, 0f);
            e.AddComponent<Enemigo>().Inicializar(Bjorn, false);
        }
        GameObject jefe = new GameObject("JefeGuardian");
        jefe.transform.position = new Vector3(101f, 0f, 0f);
        jefe.AddComponent<Enemigo>().Inicializar(Bjorn, true);
        metaEnemigos = draugr.Length + 1;

        // ---- PORTAL ----
        portal = new GameObject("Portal").transform;
        portal.position = new Vector3(113f, 0f, 0.6f);
        ConstructorPersonaje.Rect(portal, "PilarIzq", new Vector2(-1.5f, 2.2f), new Vector2(0.8f, 4.4f), new Color(0.33f, 0.36f, 0.46f), 0, 0.8f);
        ConstructorPersonaje.Rect(portal, "PilarDer", new Vector2(1.5f, 2.2f), new Vector2(0.8f, 4.4f), new Color(0.33f, 0.36f, 0.46f), 0, 0.8f);
        ConstructorPersonaje.Rect(portal, "Dintel", new Vector2(0f, 4.6f), new Vector2(4.2f, 0.7f), new Color(0.33f, 0.36f, 0.46f), 0, 0.8f);
        Transform puerta = ConstructorPersonaje.Rect(portal, "Puerta", new Vector2(0f, 2.2f), new Vector2(2.3f, 4f), new Color(0.05f, 0.06f, 0.1f), 0, 0.3f);
        puertaMR = puerta.GetComponent<MeshRenderer>();
        puertaMR.material = new Material(Shader.Find("Standard"));
        puertaMR.material.color = new Color(0.05f, 0.06f, 0.1f);
        puertaMR.material.EnableKeyword("_EMISSION");
        puertaMR.material.SetColor("_EmissionColor", Color.black);

        luzPortal = new GameObject("LuzPortal").AddComponent<Light>();
        luzPortal.transform.SetParent(portal, false);
        luzPortal.transform.localPosition = new Vector3(0f, 2.2f, -2f);
        luzPortal.type = LightType.Point;
        luzPortal.color = new Color(0.4f, 0.9f, 1f);
        luzPortal.range = 16f;
        luzPortal.intensity = 0f;

        // ---- Nieve con profundidad ----
        var nieve = new GameObject("Nieve").AddComponent<Nieve2D>();
        nieve.camara = cam.transform;
    }

    // ============================================================
    //  HISTORIA
    // ============================================================
    IEnumerator Intro()
    {
        Bjorn.controlActivo = false;
        mensajeCentral = "CAMINO AL VALHALLA\n\nCAPITULO 1: EL CAMPO DE LOS CAIDOS" +
                         (almas > 0 ? "\n\nPartida cargada - Almas: " + almas : "") +
                         "\n\n(clic para continuar)";
        Voz("av_intro1");
        yield return EsperarClic();

        mensajeCentral = "La voz de ODIN truena desde el cielo:\n\n'Los draugr profanan este campo sagrado.\nDestruyelos, y el portal se abrira.'\n\n(clic para empuñar la espada)";
        Voz("av_intro2");
        yield return EsperarClic();

        mensajeCentral = "";
        Voz("av_objetivo");
        estado = Estado.Jugando;
        Bjorn.controlActivo = true;
    }

    IEnumerator EsperarClic()
    {
        yield return null;
        while (!Input.GetMouseButtonDown(0)) yield return null;
    }

    void Voz(string nombre)
    {
        AudioClip clip = Resources.Load<AudioClip>("Voces/" + nombre);
        if (clip != null)
        {
            vozSrc.Stop();
            vozSrc.clip = clip;
            vozSrc.Play();
        }
    }

    // ============================================================
    //  EVENTOS
    // ============================================================
    public void SonidoEspada() { sfxSrc.PlayOneShot(sfxEspada); }
    public void SonidoImpacto() { sfxSrc.PlayOneShot(sfxImpacto); }
    public void SonidoTrueno() { sfxSrc.PlayOneShot(sfxTrueno); }
    public void JugadorHerido() { sfxSrc.PlayOneShot(sfxHerido); }

    public void Recoger(Recompensa r)
    {
        switch (r.tipo)
        {
            case Recompensa.Tipo.Almas:
                almas += 25;
                sfxSrc.PlayOneShot(sfxAlmas);
                Guardar();
                break;
            case Recompensa.Tipo.Pocion:
                Bjorn.vida = Mathf.Min(Bjorn.vidaMax, Bjorn.vida + 30f);
                sfxSrc.PlayOneShot(sfxAlmas);
                StartCoroutine(MensajeTemporal("+30 DE VIDA", 1.4f));
                break;
            case Recompensa.Tipo.RunaTrueno:
                Bjorn.poderRayo = true;
                sfxSrc.PlayOneShot(sfxFanfarria);
                Voz("av_poder");
                StartCoroutine(MensajeTemporal("¡PODER DESBLOQUEADO!\n\nRAYO DE ODIN - pulsa Q", 3.5f));
                Guardar();
                break;
        }
    }

    public void EnemigoMuerto(Enemigo e)
    {
        muertos++;
        almas += e.esJefe ? 500 : 100;
        sfxSrc.PlayOneShot(sfxAlmas);
        Guardar();

        if (e.esJefe)
            StartCoroutine(MensajeTemporal("¡GUARDIAN DERROTADO!\n+500 almas", 2.5f));

        if (muertos >= metaEnemigos && !portalActivo)
        {
            portalActivo = true;
            luzPortal.intensity = 6f;
            Voz("av_portal");
            StartCoroutine(MensajeTemporal("¡EL PORTAL SE ABRE!\nEntra en la luz.", 3f));
        }
    }

    public void JugadorCaido() { StartCoroutine(Respawn()); }

    IEnumerator Respawn()
    {
        Bjorn.controlActivo = false;
        Voz("av_muerte");
        mensajeCentral = "HAS CAIDO...\n\npero un einherjar siempre se levanta.";
        yield return new WaitForSeconds(3f);
        Bjorn.Teletransportar(InicioJugador);
        mensajeCentral = "";
        Bjorn.controlActivo = true;
    }

    IEnumerator MensajeTemporal(string msg, float dur)
    {
        mensajeCentral = msg;
        yield return new WaitForSeconds(dur);
        if (estado == Estado.Jugando) mensajeCentral = "";
    }

    void Guardar()
    {
        PlayerPrefs.SetInt("almas", almas);
        PlayerPrefs.Save();
        avisoGuardadoHasta = Time.time + 2f;
    }

    void Update()
    {
        musicaSrc.volume = vozSrc.isPlaying ? 0.12f : 0.3f;

        // la puerta del portal palpita con luz cian cuando esta activa
        if (portalActivo && puertaMR != null)
        {
            float t = (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f;
            Color c = Color.Lerp(new Color(0.25f, 0.8f, 0.95f), new Color(0.6f, 1f, 1f), t);
            puertaMR.material.color = c;
            puertaMR.material.SetColor("_EmissionColor", c * 1.3f);
            luzPortal.intensity = 5f + t * 3f;
        }

        if (estado == Estado.Jugando && portalActivo)
        {
            float d = Mathf.Abs(Bjorn.transform.position.x - portal.position.x);
            if (d < 1.4f)
                StartCoroutine(FinCapitulo());
        }
    }

    IEnumerator FinCapitulo()
    {
        estado = Estado.Fin;
        Bjorn.controlActivo = false;
        PlayerPrefs.SetInt("capitulo1", 1);
        Guardar();
        sfxSrc.PlayOneShot(sfxFanfarria);
        Voz("av_fin");
        mensajeCentral = "CAPITULO 1 COMPLETADO\n\nAlmas: " + almas +
                         "\nProgreso guardado\n\nCONTINUARA...";
        yield break;
    }

    // ============================================================
    //  HUD
    // ============================================================
    void OnGUI()
    {
        if (estiloCentro == null)
        {
            estiloCentro = new GUIStyle(GUI.skin.label)
            { fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            estiloCentro.normal.textColor = Color.white;
            estiloHud = new GUIStyle(GUI.skin.label)
            { fontSize = 17, fontStyle = FontStyle.Bold };
            estiloHud.normal.textColor = Color.white;
        }

        if (estado == Estado.Jugando && Bjorn != null)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(18, 18, 304, 30), texBlanca);
            GUI.color = new Color(0.85f, 0.2f, 0.15f);
            GUI.DrawTexture(new Rect(20, 20, 300f * Mathf.Clamp01(Bjorn.vida / Bjorn.vidaMax), 26), texBlanca);
            GUI.color = Color.white;
            GUI.Label(new Rect(26, 21, 300, 26), "VIDA", estiloHud);

            GUI.Label(new Rect(20, 54, 400, 30), "Almas: " + almas, estiloHud);

            GUI.Label(new Rect(Screen.width / 2 - 220, 16, 440, 30),
                      portalActivo ? "OBJETIVO: entra al portal de luz"
                                   : "OBJETIVO: acaba con los draugr  (" + muertos + "/" + metaEnemigos + ")",
                      new GUIStyle(estiloHud) { alignment = TextAnchor.MiddleCenter });

            DibujarPoder(new Rect(20, Screen.height - 96, 150, 26), "SHIFT  Dash",
                         1f - Mathf.Clamp01(Bjorn.cdDashRestante / Bjorn.cooldownDash), true);
            DibujarPoder(new Rect(20, Screen.height - 64, 150, 26),
                         Bjorn.poderRayo ? "Q  Rayo de Odin" : "?  Runa perdida...",
                         Bjorn.poderRayo ? 1f - Mathf.Clamp01(Bjorn.cdRayoRestante / Bjorn.cooldownRayo) : 0f,
                         Bjorn.poderRayo);

            GUI.Label(new Rect(20, Screen.height - 32, 800, 30),
                      "A/D: correr  |  ESPACIO: saltar  |  Clic/J: espada  |  SHIFT: dash  |  Q: poder",
                      estiloHud);

            if (Time.time < avisoGuardadoHasta)
                GUI.Label(new Rect(Screen.width - 240, 16, 220, 30), "Progreso guardado ✓", estiloHud);
        }

        if (!string.IsNullOrEmpty(mensajeCentral))
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, Screen.height * 0.30f, Screen.width, Screen.height * 0.40f), texBlanca);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.30f, Screen.width, Screen.height * 0.40f),
                      mensajeCentral, estiloCentro);
        }
    }

    void DibujarPoder(Rect r, string nombre, float carga, bool desbloqueado)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, texBlanca);
        GUI.color = desbloqueado ? new Color(1f, 0.85f, 0.2f, 0.85f) : new Color(0.4f, 0.4f, 0.4f, 0.6f);
        GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, (r.width - 4) * carga, r.height - 4), texBlanca);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 6, r.y + 3, r.width, r.height), nombre, estiloHud);
    }
}
