using UnityEngine;
using System.Collections;

/// <summary>
/// Director del juego en PRIMERA PERSONA "Camino al Valhalla - Capitulo 1":
/// - Construye el campo de batalla 3D por codigo: suelo nevado, aurora boreal,
///   luna, montanas, pilares runicos, altares de piedra, premios, draugr y jefe.
/// - Historia narrada -> exploracion y combate -> jefe -> portal -> fin.
/// - HUD con cruceta, vida, almas, poderes y guardado automatico.
/// </summary>
public class GestorAventura : MonoBehaviour
{
    public static GestorAventura Instancia;
    public Jugador Bjorn { get; private set; }

    static readonly Vector3 InicioJugador = new Vector3(-6f, 1.2f, 0f);
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

        ConstruirMundo();
        almas = PlayerPrefs.GetInt("almas", 0);
        StartCoroutine(Intro());
    }

    // ============================================================
    //  CONSTRUCCION DEL MUNDO 3D
    // ============================================================
    Transform CuboFisico(string nombre, Vector3 pos, Vector3 tam, Color color, bool visible = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); // conserva su collider 3D
        go.name = nombre;
        go.transform.position = pos;
        go.transform.localScale = tam;
        go.GetComponent<MeshRenderer>().sharedMaterial = ConstructorPersonaje.Mat(color);
        go.GetComponent<MeshRenderer>().enabled = visible;
        return go.transform;
    }

    void ConstruirMundo()
    {
        // ---- Ambiente ----
        Camera cam = Camera.main;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 300f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.19f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.10f, 0.12f, 0.25f);
        RenderSettings.fogDensity = 0.015f;
        RenderSettings.ambientLight = new Color(0.34f, 0.37f, 0.50f);

        Light sol = FindObjectOfType<Light>();
        if (sol != null)
        {
            sol.type = LightType.Directional;
            sol.color = new Color(0.82f, 0.86f, 1f);
            sol.intensity = 1.0f;
            sol.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            sol.shadows = LightShadows.Soft;
        }

        // ---- Jugador (la camara se vuelve sus ojos en su Awake) ----
        GameObject bjornGO = new GameObject("Bjorn");
        bjornGO.transform.position = InicioJugador;
        Bjorn = bjornGO.AddComponent<Jugador>();

        // ---- Suelo nevado y muros invisibles ----
        CuboFisico("Suelo", new Vector3(55f, -1f, 0f), new Vector3(170f, 2f, 80f), new Color(0.80f, 0.85f, 0.93f));
        CuboFisico("MuroOeste", new Vector3(-14f, 4f, 0f), new Vector3(1f, 12f, 80f), Color.clear, false);
        CuboFisico("MuroEste", new Vector3(124f, 4f, 0f), new Vector3(1f, 12f, 80f), Color.clear, false);
        CuboFisico("MuroNorte", new Vector3(55f, 4f, 40f), new Vector3(170f, 12f, 1f), Color.clear, false);
        CuboFisico("MuroSur", new Vector3(55f, 4f, -40f), new Vector3(170f, 12f, 1f), Color.clear, false);

        // ---- Cielo: aurora boreal y luna ----
        Color[] coloresAurora =
        {
            new Color(0.15f, 0.9f, 0.55f, 0.30f),
            new Color(0.3f, 0.75f, 1f, 0.26f),
            new Color(0.7f, 0.4f, 1f, 0.24f)
        };
        for (int i = 0; i < 3; i++)
        {
            Transform banda = ConstructorPersonaje.Rect(null, "Aurora_" + i,
                Vector2.zero, new Vector2(400f, 7f), coloresAurora[i], 0, 1f, true);
            banda.position = new Vector3(55f, 42f + i * 7f, 120f);
            banda.rotation = Quaternion.Euler(12f, 0f, -3f + i * 3f);
        }
        Transform halo = ConstructorPersonaje.Circ(null, "HaloLuna", Vector2.zero, 16f, new Color(0.9f, 0.95f, 1f, 0.18f), 0, true);
        halo.position = new Vector3(20f, 40f, 110f);
        Transform luna = ConstructorPersonaje.Circ(null, "Luna", Vector2.zero, 10f, new Color(0.95f, 0.97f, 1f), 0, true);
        luna.position = new Vector3(20f, 40f, 110f);

        // ---- Cordillera alrededor del campo ----
        for (int i = 0; i < 14; i++)
        {
            float x = -20f + i * 12f;
            Transform m1 = ConstructorPersonaje.Rect(null, "MontanaN_" + i, Vector2.zero, new Vector2(26f, 26f), new Color(0.18f, 0.23f, 0.42f), 0, 6f);
            m1.position = new Vector3(x, -4f, 60f + (i % 3) * 8f);
            m1.rotation = Quaternion.Euler(0f, 0f, 45f);
            Transform m2 = ConstructorPersonaje.Rect(null, "MontanaS_" + i, Vector2.zero, new Vector2(22f, 22f), new Color(0.15f, 0.19f, 0.37f), 0, 6f);
            m2.position = new Vector3(x, -4f, -58f - (i % 2) * 8f);
            m2.rotation = Quaternion.Euler(0f, 0f, 45f);
        }

        // ---- Pilares runicos a ambos lados del camino ----
        float[] pilaresX = { 5f, 24f, 47f, 65f, 82f, 105f };
        Color[] coloresRuna = { new Color(0.3f, 0.9f, 1f), new Color(0.4f, 1f, 0.6f), new Color(0.8f, 0.5f, 1f) };
        for (int i = 0; i < pilaresX.Length; i++)
        {
            float z = (i % 2 == 0) ? 7f : -7f;
            Transform pilar = CuboFisico("Pilar_" + i, new Vector3(pilaresX[i], 1.8f, z), new Vector3(1f, 3.6f, 1f), new Color(0.33f, 0.36f, 0.46f));
            Color runa = coloresRuna[i % coloresRuna.Length];
            Transform r1 = ConstructorPersonaje.Rect(pilar, "Runa1", new Vector2(0f, 0.22f), new Vector2(0.32f, 0.1f), runa, 0, 0.1f, true);
            r1.localPosition = new Vector3(0f, 0.22f, -0.51f);
            Transform r2 = ConstructorPersonaje.Rect(pilar, "Runa2", new Vector2(0f, 0.05f), new Vector2(0.1f, 0.3f), runa, 0, 0.1f, true);
            r2.localPosition = new Vector3(0f, 0.05f, -0.51f);
            Light luzRuna = pilar.gameObject.AddComponent<Light>();
            luzRuna.type = LightType.Point;
            luzRuna.color = runa;
            luzRuna.range = 6f;
            luzRuna.intensity = 1.6f;
        }

        // ---- Arboles nevados y rocas dispersas ----
        float[,] arboles = { {11,14}, {33,-12}, {45,16}, {59,-15}, {76,12}, {96,-10}, {30,22}, {70,20} };
        for (int i = 0; i < arboles.GetLength(0); i++)
        {
            Transform tronco = CuboFisico("Arbol_" + i, new Vector3(arboles[i, 0], 1.1f, arboles[i, 1]), new Vector3(0.4f, 2.2f, 0.4f), new Color(0.35f, 0.25f, 0.2f));
            for (int j = 0; j < 3; j++)
            {
                Transform copa = ConstructorPersonaje.Rect(tronco, "Copa_" + j, new Vector2(0f, 0.55f + j * 0.35f), new Vector2(4.6f - j * 1.2f, 1.6f), new Color(0.16f, 0.38f, 0.30f), 0, 4.6f - j * 1.2f);
                copa.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
        }
        float[,] rocas = { {16,-6}, {38,8}, {52,-9}, {74,6}, {88,-14}, {100,9}, {60,18}, {25,-18} };
        for (int i = 0; i < rocas.GetLength(0); i++)
        {
            Transform roca = CuboFisico("Roca_" + i, new Vector3(rocas[i, 0], 0.4f, rocas[i, 1]),
                new Vector3(1.3f + (i % 3) * 0.5f, 0.8f + (i % 2) * 0.6f, 1.2f), new Color(0.42f, 0.46f, 0.52f));
            roca.rotation = Quaternion.Euler(0f, i * 37f, 0f);
        }

        // ---- Altares de piedra (para saltar y alcanzar premios) ----
        float[,] altares = { {18,4}, {26,-5}, {40,6}, {55,0}, {68,-6}, {90,5} };
        for (int i = 0; i < altares.GetLength(0); i++)
            CuboFisico("Altar_" + i, new Vector3(altares[i, 0], 0.45f, altares[i, 1]), new Vector3(3f, 0.9f, 3f), new Color(0.45f, 0.49f, 0.60f));

        // ---- PREMIOS ----
        float[,] posAlmas = { {10,1,0}, {14,1,-3}, {18,2.1f,4}, {26,2.1f,-5}, {33,1,3}, {40,2.1f,6}, {47,1,-2}, {52,1,2},
                              {63,1,-4}, {68,2.1f,-6}, {76,1,3}, {90,2.1f,5}, {95,1,-2}, {106,1,0} };
        for (int i = 0; i < posAlmas.GetLength(0); i++)
            Recompensa.Crear(Recompensa.Tipo.Almas, new Vector3(posAlmas[i, 0], posAlmas[i, 1], posAlmas[i, 2]));

        Recompensa.Crear(Recompensa.Tipo.Pocion, new Vector3(35f, 1f, -6f));
        Recompensa.Crear(Recompensa.Tipo.Pocion, new Vector3(80f, 1f, 8f));
        Recompensa.Crear(Recompensa.Tipo.RunaTrueno, new Vector3(55f, 2.2f, 0f)); // sobre el altar central

        // ---- ENEMIGOS ----
        float[,] draugr = { {20,3}, {30,-4}, {42,5}, {60,-5}, {72,4}, {85,-3} };
        for (int i = 0; i < draugr.GetLength(0); i++)
        {
            GameObject e = new GameObject("Draugr_" + i);
            e.transform.position = new Vector3(draugr[i, 0], 0f, draugr[i, 1]);
            e.AddComponent<Enemigo>().Inicializar(Bjorn, false);
        }
        GameObject jefe = new GameObject("JefeGuardian");
        jefe.transform.position = new Vector3(101f, 0f, 0f);
        jefe.AddComponent<Enemigo>().Inicializar(Bjorn, true);
        metaEnemigos = draugr.GetLength(0) + 1;

        // ---- PORTAL ----
        portal = new GameObject("Portal").transform;
        portal.position = new Vector3(113f, 0f, 0f);
        CuboFisico("PilarIzq", portal.position + new Vector3(0f, 2.2f, -1.8f), new Vector3(0.9f, 4.4f, 0.9f), new Color(0.33f, 0.36f, 0.46f)).SetParent(portal);
        CuboFisico("PilarDer", portal.position + new Vector3(0f, 2.2f, 1.8f), new Vector3(0.9f, 4.4f, 0.9f), new Color(0.33f, 0.36f, 0.46f)).SetParent(portal);
        CuboFisico("Dintel", portal.position + new Vector3(0f, 4.6f, 0f), new Vector3(0.9f, 0.7f, 4.8f), new Color(0.33f, 0.36f, 0.46f)).SetParent(portal);
        Transform puerta = ConstructorPersonaje.Rect(portal, "Puerta", Vector2.zero, new Vector2(0.3f, 4f), new Color(0.05f, 0.06f, 0.1f), 0, 2.8f);
        puerta.position = portal.position + new Vector3(0f, 2.2f, 0f);
        puertaMR = puerta.GetComponent<MeshRenderer>();
        puertaMR.material = new Material(Shader.Find("Standard"));
        puertaMR.material.color = new Color(0.05f, 0.06f, 0.1f);
        puertaMR.material.EnableKeyword("_EMISSION");
        puertaMR.material.SetColor("_EmissionColor", Color.black);

        luzPortal = new GameObject("LuzPortal").AddComponent<Light>();
        luzPortal.transform.SetParent(portal, false);
        luzPortal.transform.localPosition = new Vector3(-2f, 2.2f, 0f);
        luzPortal.type = LightType.Point;
        luzPortal.color = new Color(0.4f, 0.9f, 1f);
        luzPortal.range = 18f;
        luzPortal.intensity = 0f;

        // ---- Nieve que te sigue ----
        var nieve = new GameObject("Nieve").AddComponent<Nieve>();
        nieve.centro = bjornGO.transform;
    }

    // ============================================================
    //  HISTORIA
    // ============================================================
    IEnumerator Intro()
    {
        Cursor.lockState = CursorLockMode.None;
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
        Cursor.lockState = CursorLockMode.Locked;
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

        // ESC libera el raton; clic lo captura de nuevo
        if (Input.GetKeyDown(KeyCode.Escape))
            Cursor.lockState = CursorLockMode.None;
        if (estado == Estado.Jugando && Input.GetMouseButtonDown(0) &&
            Cursor.lockState != CursorLockMode.Locked)
            Cursor.lockState = CursorLockMode.Locked;

        // portal palpitante
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
            Vector3 d = Bjorn.transform.position - portal.position;
            d.y = 0f;
            if (d.magnitude < 2.2f)
                StartCoroutine(FinCapitulo());
        }
    }

    IEnumerator FinCapitulo()
    {
        estado = Estado.Fin;
        Bjorn.controlActivo = false;
        Cursor.lockState = CursorLockMode.None;
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
            // cruceta de mira
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(cx - 8, cy - 1, 16, 2), texBlanca);
            GUI.DrawTexture(new Rect(cx - 1, cy - 8, 2, 16), texBlanca);
            GUI.color = Color.white;

            // barra de vida
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

            GUI.Label(new Rect(20, Screen.height - 32, 900, 30),
                      "WASD: moverse  |  Raton: mirar  |  Clic: espada  |  ESPACIO: saltar  |  SHIFT: dash  |  Q: poder  |  ESC: liberar raton",
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
