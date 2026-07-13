using UnityEngine;
using System.Collections;

/// <summary>
/// Director de "Camino al Valhalla" en primera persona.
/// MODO HISTORIA: campo de los caidos, misiones, jefe, portal y resultados animados.
/// MODO DESAFIO DEL DIA: oleadas con semilla diaria (el mismo reto para todos) y record.
/// Extras: tienda-arbol y LOGROS (TAB), racha de almas, cofres, hogueras de checkpoint,
/// dificultad adaptativa invisible y guardado automatico.
/// </summary>
public class GestorAventura : MonoBehaviour
{
    public static GestorAventura Instancia;
    public Jugador Bjorn { get; private set; }

    static readonly Vector3 InicioJugador = new Vector3(-6f, 1.2f, 0f);
    const int TotalAlmasDelCampo = 14;
    int metaEnemigos;

    Transform portal;
    MeshRenderer puertaMR;
    Light luzPortal;
    bool portalActivo;

    int almas;
    int muertos;

    // racha
    int racha;
    int Multiplicador { get { return 1 + Mathf.Min(2, racha / 2); } }

    // misiones (modo historia)
    bool sinDano = true;
    int almasRecogidas;
    float inicioNivel;
    const float TiempoMision = 300f;

    // dificultad adaptativa invisible
    int muertesSesion;

    // desafio del dia
    bool modoDesafio;
    int oleada;
    int puntuacion;
    string claveRecord;

    enum Estado { Intro, Jugando, Fin }
    Estado estado = Estado.Intro;
    string mensajeCentral = "";       // solo para intro / fin (pantallas modales)
    string avisoEsquina = "";         // avisos de juego: pergamino en la esquina
    float avisoEsquinaHasta;
    float avisoGuardadoHasta;
    bool tiendaAbierta;
    int pestanaPanel; // 0 = habilidades, 1 = logros, 2 = poderes

    class Habilidad
    {
        public string nombre, desc;
        public int nivel, nivMax, costoBase;
        public int Costo { get { return costoBase * (nivel + 1); } }
        public Habilidad(string n, string d, int max, int costo) { nombre = n; desc = d; nivMax = max; costoBase = costo; }
    }
    static readonly string[] NombresRamas = { "GUERRERO", "TORMENTA", "EINHERJAR" };
    Habilidad[][] habilidades;

    AudioSource musicaSrc, sfxSrc;
    AudioClip sfxEspada, sfxImpacto, sfxHerido, sfxFanfarria, sfxAlmas, sfxTrueno, sfxTecla, sfxRemate;
    Texture2D texBlanca;
    GUIStyle estiloCentro, estiloHud, estiloMision, estiloTitulo;

    void Awake() { Instancia = this; }

    void Start()
    {
        musicaSrc = gameObject.AddComponent<AudioSource>();
        musicaSrc.clip = GeneradorAudio.MusicaNordica();
        musicaSrc.loop = true;
        musicaSrc.volume = 0.3f;
        musicaSrc.Play();
        sfxSrc = gameObject.AddComponent<AudioSource>();
        sfxEspada = GeneradorAudio.Espada();
        sfxImpacto = GeneradorAudio.Impacto();
        sfxHerido = GeneradorAudio.Herido();
        sfxFanfarria = GeneradorAudio.Fanfarria();
        sfxAlmas = GeneradorAudio.Almas();
        sfxTrueno = GeneradorAudio.Trueno();
        sfxTecla = GeneradorAudio.Tecla();
        sfxRemate = GeneradorAudio.Remate();

        texBlanca = new Texture2D(1, 1);
        texBlanca.SetPixel(0, 0, Color.white);
        texBlanca.Apply();

        System.DateTime hoy = System.DateTime.Now;
        claveRecord = "record_" + hoy.Year + "_" + hoy.DayOfYear;

        DefinirHabilidades();
        ConstruirMundo();
        Hoguera.PuntoRespawn = InicioJugador;
        Enemigo.FactorDanoGlobal = 1f;
        almas = PlayerPrefs.GetInt("almas", 0);
        CargarHabilidades();
        AplicarHabilidades();
        StartCoroutine(Intro());
    }

    // ============================================================
    //  HABILIDADES
    // ============================================================
    void DefinirHabilidades()
    {
        habilidades = new Habilidad[3][];
        habilidades[0] = new[]
        {
            new Habilidad("Vigor",         "+30 de vida maxima",            3, 100),
            new Habilidad("Filo",          "+20% de dano de espada",        3, 150),
            new Habilidad("Remate Brutal", "El 3er golpe pega +50% extra",  1, 300)
        };
        habilidades[1] = new[]
        {
            new Habilidad("Rayo Veloz",      "-0.8s de recarga del rayo",        2, 150),
            new Habilidad("Rayo Penetrante", "El rayo ATRAVIESA enemigos",       1, 350),
            new Habilidad("Trueno de Odin",  "Al golpear cae un trueno en area", 1, 500)
        };
        habilidades[2] = new[]
        {
            new Habilidad("Piel de Hierro",  "-15% de dano recibido",      2, 150),
            new Habilidad("Viento del Norte","-0.25s de recarga del dash", 2, 120),
            new Habilidad("Paso Ligero",     "+8% de velocidad",           2, 120)
        };
    }

    void CargarHabilidades()
    {
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                habilidades[r][c].nivel = PlayerPrefs.GetInt("hab_" + r + "_" + c, 0);
    }

    void AplicarHabilidades()
    {
        Habilidad[] g = habilidades[0], t = habilidades[1], e = habilidades[2];
        float vidaAnterior = Bjorn.vidaMax;
        Bjorn.vidaMax = 100f + 30f * g[0].nivel;
        Bjorn.vida += Mathf.Max(0f, Bjorn.vidaMax - vidaAnterior);
        Bjorn.danoMultiplicador = 1f + 0.2f * g[1].nivel;
        Bjorn.bonoRemate = 1.7f + 0.5f * g[2].nivel;
        Bjorn.cooldownRayo = 3.5f - 0.8f * t[0].nivel;
        Bjorn.nivelRayo = 1 + t[1].nivel + t[2].nivel;
        Bjorn.reduccionDano = 0.15f * e[0].nivel;
        Bjorn.cooldownDash = 1.2f - 0.25f * e[1].nivel;
        Bjorn.velocidad = 6.5f * (1f + 0.08f * e[2].nivel);
    }

    void ComprarHabilidad(int rama, int idx)
    {
        Habilidad h = habilidades[rama][idx];
        if (h.nivel >= h.nivMax || almas < h.Costo) return;
        almas -= h.Costo;
        h.nivel++;
        PlayerPrefs.SetInt("hab_" + rama + "_" + idx, h.nivel);
        AplicarHabilidades();
        Guardar();
        sfxSrc.PlayOneShot(sfxFanfarria);

        Logros.Desbloquear("comprador");
        if (rama == 1 && idx == 2) Logros.Desbloquear("trueno");
        bool todo = true;
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                if (habilidades[r][c].nivel < habilidades[r][c].nivMax) todo = false;
        if (todo) Logros.Desbloquear("arbol");
    }

    // ============================================================
    //  MUNDO
    // ============================================================
    Transform CuboFisico(string nombre, Vector3 pos, Vector3 tam, Color color, bool visible = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.position = pos;
        go.transform.localScale = tam;
        go.GetComponent<MeshRenderer>().sharedMaterial = ConstructorPersonaje.Mat(color);
        go.GetComponent<MeshRenderer>().enabled = visible;
        return go.transform;
    }

    void ConstruirMundo()
    {
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

        GameObject bjornGO = new GameObject("Bjorn");
        bjornGO.transform.position = InicioJugador;
        Bjorn = bjornGO.AddComponent<Jugador>();

        CuboFisico("Suelo", new Vector3(55f, -1f, 0f), new Vector3(170f, 2f, 80f), new Color(0.80f, 0.85f, 0.93f));
        CuboFisico("MuroOeste", new Vector3(-14f, 4f, 0f), new Vector3(1f, 12f, 80f), Color.clear, false);
        CuboFisico("MuroEste", new Vector3(124f, 4f, 0f), new Vector3(1f, 12f, 80f), Color.clear, false);
        CuboFisico("MuroNorte", new Vector3(55f, 4f, 40f), new Vector3(170f, 12f, 1f), Color.clear, false);
        CuboFisico("MuroSur", new Vector3(55f, 4f, -40f), new Vector3(170f, 12f, 1f), Color.clear, false);

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

        float[] pilaresX = { 5f, 24f, 47f, 65f, 82f, 105f };
        Color[] coloresRuna = { new Color(0.3f, 0.9f, 1f), new Color(0.4f, 1f, 0.6f), new Color(0.8f, 0.5f, 1f) };
        for (int i = 0; i < pilaresX.Length; i++)
        {
            float z = (i % 2 == 0) ? 7f : -7f;
            Transform pilar = CuboFisico("Pilar_" + i, new Vector3(pilaresX[i], 1.8f, z), new Vector3(1f, 3.6f, 1f), new Color(0.33f, 0.36f, 0.46f));
            Color runa = coloresRuna[i % coloresRuna.Length];
            Transform r1 = ConstructorPersonaje.Rect(pilar, "Runa1", Vector2.zero, new Vector2(0.32f, 0.1f), runa, 0, 0.1f, true);
            r1.localPosition = new Vector3(0f, 0.22f, -0.51f);
            Transform r2 = ConstructorPersonaje.Rect(pilar, "Runa2", Vector2.zero, new Vector2(0.1f, 0.3f), runa, 0, 0.1f, true);
            r2.localPosition = new Vector3(0f, 0.05f, -0.51f);
            Light luzRuna = pilar.gameObject.AddComponent<Light>();
            luzRuna.type = LightType.Point;
            luzRuna.color = runa;
            luzRuna.range = 6f;
            luzRuna.intensity = 1.6f;
        }

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

        float[,] altares = { {18,4}, {26,-5}, {40,6}, {55,0}, {68,-6}, {90,5} };
        for (int i = 0; i < altares.GetLength(0); i++)
            CuboFisico("Altar_" + i, new Vector3(altares[i, 0], 0.45f, altares[i, 1]), new Vector3(3f, 0.9f, 3f), new Color(0.45f, 0.49f, 0.60f));

        float[,] posAlmas = { {10,1,0}, {14,1,-3}, {18,2.1f,4}, {26,2.1f,-5}, {33,1,3}, {40,2.1f,6}, {47,1,-2}, {52,1,2},
                              {63,1,-4}, {68,2.1f,-6}, {76,1,3}, {90,2.1f,5}, {95,1,-2}, {106,1,0} };
        for (int i = 0; i < posAlmas.GetLength(0); i++)
            Recompensa.Crear(Recompensa.Tipo.Almas, new Vector3(posAlmas[i, 0], posAlmas[i, 1], posAlmas[i, 2]));

        Recompensa.Crear(Recompensa.Tipo.Pocion, new Vector3(35f, 1f, -6f));
        Recompensa.Crear(Recompensa.Tipo.Pocion, new Vector3(80f, 1f, 8f));
        Recompensa.Crear(Recompensa.Tipo.RunaTrueno, new Vector3(55f, 2.2f, 0f));

        Cofre.Crear(new Vector3(28f, 0.05f, -9f));
        Cofre.Crear(new Vector3(58f, 0.05f, 11f));
        Cofre.Crear(new Vector3(92f, 0.05f, -8f));

        // hogueras de checkpoint
        Hoguera.Crear(new Vector3(30f, 0.05f, 2f));
        Hoguera.Crear(new Vector3(65f, 0.05f, -2f));
        Hoguera.Crear(new Vector3(95f, 0.05f, 3f));

        float[,] draugr = { {20,3}, {30,-4}, {42,5}, {60,-5}, {72,4}, {85,-3} };
        for (int i = 0; i < draugr.GetLength(0); i++)
            CrearDraugr(new Vector3(draugr[i, 0], 0f, draugr[i, 1]), false);
        CrearDraugr(new Vector3(101f, 0f, 0f), true);
        metaEnemigos = draugr.GetLength(0) + 1;

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

        var nieve = new GameObject("Nieve").AddComponent<Nieve>();
        nieve.centro = bjornGO.transform;
    }

    Enemigo CrearDraugr(Vector3 pos, bool jefe)
    {
        GameObject e = new GameObject(jefe ? "JefeGuardian" : "Draugr");
        e.transform.position = pos;
        Enemigo en = e.AddComponent<Enemigo>();
        en.Inicializar(Bjorn, jefe);
        return en;
    }

    // ============================================================
    //  DIALOGOS letra a letra
    // ============================================================
    IEnumerator MostrarDialogo(string texto)
    {
        int i = 0;
        float acumulado = 0f;
        const float porLetra = 0.024f;

        while (i < texto.Length)
        {
            if (Input.GetMouseButtonDown(0)) { i = texto.Length; break; }
            acumulado += Time.deltaTime;
            while (acumulado >= porLetra && i < texto.Length)
            {
                acumulado -= porLetra;
                i++;
                if (i % 2 == 0 && texto[i - 1] != ' ' && texto[i - 1] != '\n')
                    sfxSrc.PlayOneShot(sfxTecla, 0.5f);
            }
            mensajeCentral = texto.Substring(0, i);
            yield return null;
        }

        mensajeCentral = texto + "\n\n- clic para continuar -";
        yield return null;
        while (!Input.GetMouseButtonDown(0)) yield return null;
        mensajeCentral = "";
    }

    IEnumerator Intro()
    {
        Cursor.lockState = CursorLockMode.None;
        Bjorn.controlActivo = false;

        // portada: elegir modo
        int record = PlayerPrefs.GetInt(claveRecord, 0);
        string portada = "CAMINO AL VALHALLA" +
                         (almas > 0 ? "\n\nPartida cargada - Almas: " + almas : "") +
                         "\n\nCLIC  -  Capitulo 1: El campo de los caidos" +
                         "\nD  -  DESAFIO DEL DIA (oleadas)" +
                         (record > 0 ? "  |  Record de hoy: " + record : "") +
                         "\n\nLogros: " + Logros.TotalDesbloqueados() + "/20";
        mensajeCentral = portada;

        bool desafio = false;
        yield return null;
        while (true)
        {
            if (Input.GetMouseButtonDown(0)) break;
            if (Input.GetKeyDown(KeyCode.D)) { desafio = true; break; }
            yield return null;
        }
        mensajeCentral = "";

        if (desafio)
        {
            StartCoroutine(ModoDesafio());
            yield break;
        }

        yield return MostrarDialogo(
            "La voz de ODIN truena desde el cielo:\n\n'Los draugr profanan este campo sagrado.\nDestruyelos, y el portal se abrira.'");

        yield return MostrarDialogo(
            "MISIONES DEL CAPITULO:\n\n- No recibas dano (+100 almas)\n- Recoge las 14 runas doradas (+100)\n- Termina en menos de 5 min (+100)\n\nTAB: habilidades y logros\nLas hogueras curan y guardan tu posicion.");

        estado = Estado.Jugando;
        inicioNivel = Time.time;
        Bjorn.controlActivo = true;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // ============================================================
    //  DESAFIO DEL DIA (semilla diaria: el mismo reto para todos)
    // ============================================================
    IEnumerator ModoDesafio()
    {
        modoDesafio = true;
        puntuacion = 0;

        // limpiar el modo historia: draugr y jefe originales fuera
        foreach (Enemigo e in Enemigo.Todos.ToArray())
            if (e != null) Destroy(e.gameObject);

        yield return MostrarDialogo(
            "DESAFIO DEL DIA\n\n5 oleadas de draugr, iguales para todo el mundo hoy.\nCada alma cuenta. Si caes, se acabo.\n\n¡Demuestra tu valor!");

        estado = Estado.Jugando;
        inicioNivel = Time.time;
        Bjorn.controlActivo = true;
        Cursor.lockState = CursorLockMode.Locked;

        System.DateTime hoy = System.DateTime.Now;
        var azar = new System.Random(hoy.Year * 1000 + hoy.DayOfYear); // semilla del dia

        for (oleada = 1; oleada <= 5; oleada++)
        {
            StartCoroutine(MensajeTemporal("OLEADA " + oleada + " / 5", 2f));
            int cantidad = 2 + oleada;
            for (int i = 0; i < cantidad; i++)
            {
                float x = 10f + (float)azar.NextDouble() * 90f;
                float z = -14f + (float)azar.NextDouble() * 28f;
                bool jefe = oleada == 5 && i == 0; // la ultima oleada trae un guardian
                CrearDraugr(new Vector3(x, 0f, z), jefe);
            }

            // esperar a que caigan todos
            while (true)
            {
                bool quedan = false;
                foreach (Enemigo e in Enemigo.Todos)
                    if (e != null && !e.muerto) { quedan = true; break; }
                if (!quedan) break;
                if (estado == Estado.Fin) yield break; // el jugador cayo
                yield return new WaitForSeconds(0.3f);
            }
        }

        StartCoroutine(FinDesafio(true));
    }

    IEnumerator FinDesafio(bool victoria)
    {
        if (estado == Estado.Fin) yield break;
        estado = Estado.Fin;
        Bjorn.controlActivo = false;
        Cursor.lockState = CursorLockMode.None;

        int record = PlayerPrefs.GetInt(claveRecord, 0);
        bool nuevoRecord = puntuacion > record;
        if (nuevoRecord) { PlayerPrefs.SetInt(claveRecord, puntuacion); PlayerPrefs.Save(); }
        sfxSrc.PlayOneShot(victoria ? sfxFanfarria : sfxHerido);

        mensajeCentral =
            (victoria ? "¡DESAFIO SUPERADO!" : "HAS CAIDO EN LA OLEADA " + oleada) +
            "\n\nPuntos de hoy: " + puntuacion +
            "\nRecord de hoy: " + Mathf.Max(record, puntuacion) +
            (nuevoRecord ? "\n\n¡NUEVO RECORD!" : "") +
            "\n\n(cierra y vuelve a dar Play para otra ronda)";
        yield break;
    }

    // ============================================================
    //  EVENTOS
    // ============================================================
    public void SonidoEspada() { sfxSrc.PlayOneShot(sfxEspada); }
    public void SonidoImpacto() { sfxSrc.PlayOneShot(sfxImpacto); }
    public void SonidoTrueno() { sfxSrc.PlayOneShot(sfxTrueno); }
    public void SonidoRemate() { sfxSrc.PlayOneShot(sfxRemate); }
    public void SonidoParry() { sfxSrc.PlayOneShot(sfxEspada); sfxSrc.PlayOneShot(sfxImpacto); }
    /// <summary>Aviso en el pergamino de la esquina (no tapa la vista).</summary>
    public void MostrarAviso(string msg)
    {
        avisoEsquina = msg;
        avisoEsquinaHasta = Time.time + 2.2f;
    }

    public void LogroDesbloqueado(string nombre)
    {
        sfxSrc.PlayOneShot(sfxFanfarria);
        StartCoroutine(MensajeTemporal("LOGRO DESBLOQUEADO\n\n" + nombre, 2.2f));
    }

    public void HogueraEncendida()
    {
        sfxSrc.PlayOneShot(sfxFanfarria);
        MostrarAviso("Hoguera encendida: vida restaurada\ny punto de descanso guardado");
    }

    public void JugadorHerido()
    {
        sfxSrc.PlayOneShot(sfxHerido);
        sinDano = false;
        if (racha > 0) MostrarAviso("Racha perdida...");
        racha = 0;
    }

    public void Recoger(Recompensa r)
    {
        switch (r.tipo)
        {
            case Recompensa.Tipo.Almas:
                almasRecogidas++;
                GanarAlmas(25);
                break;
            case Recompensa.Tipo.Pocion:
                Bjorn.vida = Mathf.Min(Bjorn.vidaMax, Bjorn.vida + 30f);
                sfxSrc.PlayOneShot(sfxAlmas);
                MostrarAviso("+30 DE VIDA");
                break;
            case Recompensa.Tipo.RunaTrueno:
                Bjorn.poderRayo = true;
                sfxSrc.PlayOneShot(sfxFanfarria);
                StartCoroutine(MensajeTemporal("¡PODER DESBLOQUEADO!\n\nRAYO DE ODIN - pulsa Q\n(mejoralo en la tienda con TAB)", 3.5f));
                Guardar();
                break;
        }
    }

    void GanarAlmas(int base_)
    {
        int ganadas = base_ * Multiplicador;
        almas += ganadas;
        if (modoDesafio) puntuacion += ganadas;
        sfxSrc.pitch = 1f + (Multiplicador - 1) * 0.15f;
        sfxSrc.PlayOneShot(sfxAlmas);
        sfxSrc.pitch = 1f;
        Guardar();

        int total = Logros.Contar("almas", ganadas);
        if (total >= 1000) Logros.Desbloquear("rico");
        if (total >= 3000) Logros.Desbloquear("millonario");
    }

    public void CofreAbierto(string msg)
    {
        sfxSrc.PlayOneShot(sfxFanfarria);
        MostrarAviso(msg);
        if (Logros.Contar("cofres", 1) >= 3) Logros.Desbloquear("abridor");
    }

    public void CofreGema()
    {
        sfxSrc.PlayOneShot(sfxFanfarria);
        GanarAlmas(150);
        MostrarAviso("¡GEMA RARA! +150 almas");
        if (Logros.Contar("cofres", 1) >= 3) Logros.Desbloquear("abridor");
    }

    public void EnemigoMuerto(Enemigo e)
    {
        muertos++;
        racha++;
        GanarAlmas(e.esJefe ? 500 : 100);

        int total = Logros.Contar("draugr", 1);
        if (total >= 1) Logros.Desbloquear("primera_sangre");
        if (total >= 10) Logros.Desbloquear("cazador");
        if (total >= 50) Logros.Desbloquear("exterminador");
        if (e.esJefe) Logros.Desbloquear("jefe");

        if (e.esJefe)
            StartCoroutine(MensajeTemporal("¡GUARDIAN DERROTADO!", 2.5f));

        if (!modoDesafio && muertos >= metaEnemigos && !portalActivo)
        {
            portalActivo = true;
            luzPortal.intensity = 6f;
            StartCoroutine(MensajeTemporal("¡EL PORTAL SE ABRE!\nEntra en la luz.", 3f));
        }
    }

    public void JugadorCaido()
    {
        if (modoDesafio) { StartCoroutine(FinDesafio(false)); return; }
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        Bjorn.controlActivo = false;
        mensajeCentral = "HAS CAIDO...\n\npero un einherjar siempre se levanta.";

        // dificultad adaptativa invisible: si mueres seguido, el juego afloja un poco
        muertesSesion++;
        if (muertesSesion >= 2)
        {
            Enemigo.FactorDanoGlobal = 0.85f;
            Recompensa.Crear(Recompensa.Tipo.Pocion, Hoguera.PuntoRespawn + new Vector3(1.5f, 0f, 1f));
        }

        yield return new WaitForSeconds(2.5f);
        Bjorn.Teletransportar(Hoguera.PuntoRespawn);
        mensajeCentral = "";
        Bjorn.controlActivo = true;
    }

    IEnumerator MensajeTemporal(string msg, float dur)
    {
        // ahora todos los mensajes temporales van al pergamino de la esquina
        avisoEsquina = msg;
        avisoEsquinaHasta = Time.time + dur;
        yield break;
    }

    void Guardar()
    {
        PlayerPrefs.SetInt("almas", almas);
        PlayerPrefs.Save();
        avisoGuardadoHasta = Time.time + 2f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Cursor.lockState = CursorLockMode.None;
        if (estado == Estado.Jugando && !tiendaAbierta && Input.GetMouseButtonDown(0) &&
            Cursor.lockState != CursorLockMode.Locked)
            Cursor.lockState = CursorLockMode.Locked;

        // TAB abre habilidades; E abre directamente la pestaña de PODERES
        if (estado == Estado.Jugando && (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.E)))
        {
            if (!tiendaAbierta)
                pestanaPanel = Input.GetKeyDown(KeyCode.E) ? 2 : 0;
            tiendaAbierta = !tiendaAbierta;
            Bjorn.controlActivo = !tiendaAbierta;
            Cursor.lockState = tiendaAbierta ? CursorLockMode.None : CursorLockMode.Locked;
        }

        if (portalActivo && puertaMR != null)
        {
            float t = (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f;
            Color c = Color.Lerp(new Color(0.25f, 0.8f, 0.95f), new Color(0.6f, 1f, 1f), t);
            puertaMR.material.color = c;
            puertaMR.material.SetColor("_EmissionColor", c * 1.3f);
            luzPortal.intensity = 5f + t * 3f;
        }

        if (estado == Estado.Jugando && portalActivo && !tiendaAbierta)
        {
            Vector3 d = Bjorn.transform.position - portal.position;
            d.y = 0f;
            if (d.magnitude < 2.2f)
                StartCoroutine(FinCapitulo());
        }
    }

    // ============================================================
    //  FIN DE CAPITULO con resultados ANIMADOS
    // ============================================================
    IEnumerator FinCapitulo()
    {
        estado = Estado.Fin;
        Bjorn.controlActivo = false;
        Cursor.lockState = CursorLockMode.None;
        PlayerPrefs.SetInt("capitulo1", 1);

        float tiempo = Time.time - inicioNivel;
        bool m1 = sinDano;
        bool m2 = almasRecogidas >= TotalAlmasDelCampo;
        bool m3 = tiempo <= TiempoMision;
        int cumplidas = (m1 ? 1 : 0) + (m2 ? 1 : 0) + (m3 ? 1 : 0);
        almas += cumplidas * 100;
        Guardar();

        if (m1) Logros.Desbloquear("intocable");
        if (m2) Logros.Desbloquear("codicioso");
        if (m3) Logros.Desbloquear("veloz");
        string rango = cumplidas == 3 ? "VALHALLA" : cumplidas == 2 ? "ORO" : cumplidas == 1 ? "PLATA" : "BRONCE";
        if (cumplidas == 3) Logros.Desbloquear("valhalla");

        // ---- presentacion animada, linea por linea ----
        string encabezado = "CAPITULO 1 COMPLETADO\n\n";
        mensajeCentral = encabezado;
        sfxSrc.PlayOneShot(sfxFanfarria);
        yield return new WaitForSeconds(0.7f);

        // el tiempo "corre" hasta su valor real
        for (float f = 0f; f <= 1f; f += Time.deltaTime * 1.6f)
        {
            float tMostrado = tiempo * f;
            mensajeCentral = encabezado + "Tiempo: " + Reloj(tMostrado);
            if (Time.frameCount % 3 == 0) sfxSrc.PlayOneShot(sfxTecla, 0.4f);
            yield return null;
        }
        string acumulado = encabezado + "Tiempo: " + Reloj(tiempo) + "\n\n";
        mensajeCentral = acumulado;
        yield return new WaitForSeconds(0.35f);

        // misiones, una por una
        string[] lineas =
        {
            (m1 ? "[X]" : "[ ]") + " Sin recibir dano",
            (m2 ? "[X]" : "[ ]") + " Las 14 runas doradas (" + almasRecogidas + "/14)",
            (m3 ? "[X]" : "[ ]") + " Menos de 5 minutos"
        };
        bool[] ok = { m1, m2, m3 };
        for (int i = 0; i < 3; i++)
        {
            acumulado += lineas[i] + "\n";
            mensajeCentral = acumulado;
            sfxSrc.PlayOneShot(ok[i] ? sfxAlmas : sfxImpacto, 0.8f);
            yield return new WaitForSeconds(0.45f);
        }

        // bonus que sube
        for (float f = 0f; f <= 1f; f += Time.deltaTime * 1.8f)
        {
            mensajeCentral = acumulado + "\nBonus: +" + Mathf.RoundToInt(cumplidas * 100 * f) + " almas   |   Total: " + almas;
            yield return null;
        }
        acumulado += "\nBonus: +" + (cumplidas * 100) + " almas   |   Total: " + almas + "\n\n";
        mensajeCentral = acumulado;
        yield return new WaitForSeconds(0.5f);

        // rango final con fanfarria
        sfxSrc.PlayOneShot(sfxFanfarria);
        mensajeCentral = acumulado + "RANGO: " + rango + "\n\nCONTINUARA...";
    }

    string Reloj(float t)
    {
        return Mathf.FloorToInt(t / 60f) + ":" + Mathf.FloorToInt(t % 60f).ToString("00");
    }

    // ============================================================
    //  HUD + PANEL (habilidades / logros)
    // ============================================================
    void OnGUI()
    {
        if (estiloCentro == null)
        {
            estiloCentro = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            estiloCentro.normal.textColor = Color.white;
            estiloHud = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            estiloHud.normal.textColor = Color.white;
            estiloMision = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            estiloMision.normal.textColor = new Color(0.8f, 0.9f, 1f);
            estiloTitulo = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            estiloTitulo.normal.textColor = new Color(1f, 0.85f, 0.3f);
        }

        if (estado == Estado.Jugando && Bjorn != null && !tiendaAbierta)
            DibujarHUD();

        if (tiendaAbierta)
            DibujarPanel();

        // avisos de juego: pergamino nordico en la esquina inferior derecha
        if (Time.time < avisoEsquinaHasta && !tiendaAbierta && estado == Estado.Jugando)
            DibujarPergamino();

        // pantallas modales (intro / fin): centradas, con el juego detenido
        if (!string.IsNullOrEmpty(mensajeCentral) && !tiendaAbierta)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, Screen.height * 0.26f, Screen.width, Screen.height * 0.48f), texBlanca);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.26f, Screen.width, Screen.height * 0.48f), mensajeCentral, estiloCentro);
        }
    }

    /// <summary>Pergamino viejo nordico en la esquina: fondo beige, borde de madera y runas.</summary>
    void DibujarPergamino()
    {
        float w = 330f, h = 108f;
        float x = Screen.width - w - 24f;
        float y = Screen.height - h - 110f;

        // desvanecimiento en el ultimo medio segundo
        float alfa = Mathf.Clamp01((avisoEsquinaHasta - Time.time) / 0.5f);

        // borde de madera oscura
        GUI.color = new Color(0.30f, 0.20f, 0.10f, alfa);
        GUI.DrawTexture(new Rect(x - 5, y - 5, w + 10, h + 10), texBlanca);
        // papel viejo
        GUI.color = new Color(0.85f, 0.77f, 0.58f, alfa);
        GUI.DrawTexture(new Rect(x, y, w, h), texBlanca);
        // esquinas "rasgadas"
        GUI.color = new Color(0.30f, 0.20f, 0.10f, alfa);
        GUI.DrawTexture(new Rect(x, y, 14, 14), texBlanca);
        GUI.DrawTexture(new Rect(x + w - 14, y + h - 14, 14, 14), texBlanca);
        // cenefa runica superior
        GUI.color = new Color(0.45f, 0.30f, 0.15f, alfa);
        GUI.DrawTexture(new Rect(x + 18, y + 8, w - 36, 2), texBlanca);
        GUI.DrawTexture(new Rect(x + 18, y + h - 10, w - 36, 2), texBlanca);

        // texto en tinta oscura
        GUIStyle tinta = new GUIStyle(GUI.skin.label)
        { fontSize = 15, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
        tinta.normal.textColor = new Color(0.25f, 0.15f, 0.06f, alfa);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 14, y + 10, w - 28, h - 20), avisoEsquina, tinta);
    }

    void DibujarHUD()
    {
        float cx = Screen.width / 2f, cy = Screen.height / 2f;
        GUI.color = new Color(1f, 1f, 1f, 0.8f);
        GUI.DrawTexture(new Rect(cx - 8, cy - 1, 16, 2), texBlanca);
        GUI.DrawTexture(new Rect(cx - 1, cy - 8, 2, 16), texBlanca);
        GUI.color = Color.white;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(18, 18, 304, 30), texBlanca);
        GUI.color = new Color(0.85f, 0.2f, 0.15f);
        GUI.DrawTexture(new Rect(20, 20, 300f * Mathf.Clamp01(Bjorn.vida / Bjorn.vidaMax), 26), texBlanca);
        GUI.color = Color.white;
        GUI.Label(new Rect(26, 21, 300, 26), "VIDA  " + Mathf.CeilToInt(Bjorn.vida) + "/" + Mathf.CeilToInt(Bjorn.vidaMax), estiloHud);

        string racha_ = Multiplicador > 1 ? "   RACHA x" + Multiplicador + "!" : "";
        GUI.Label(new Rect(20, 54, 400, 30), "Almas: " + almas + racha_, estiloHud);

        string objetivo;
        if (modoDesafio)
            objetivo = "OLEADA " + oleada + "/5      PUNTOS: " + puntuacion;
        else
            objetivo = portalActivo ? "OBJETIVO: entra al portal de luz"
                                    : "OBJETIVO: acaba con los draugr  (" + muertos + "/" + metaEnemigos + ")";
        GUI.Label(new Rect(Screen.width / 2 - 220, 16, 440, 30), objetivo,
                  new GUIStyle(estiloHud) { alignment = TextAnchor.MiddleCenter });

        if (!modoDesafio)
        {
            float tiempo = Time.time - inicioNivel;
            GUI.Label(new Rect(Screen.width - 320, 50, 300, 90),
                      (sinDano ? "O" : "X") + " Sin recibir dano\n" +
                      "O Runas doradas " + almasRecogidas + "/14\n" +
                      (tiempo <= TiempoMision ? "O" : "X") + " Tiempo " + Reloj(tiempo) + " / 5:00",
                      estiloMision);
        }

        if (Time.time < Bjorn.comboHasta && Bjorn.combo > 1)
            GUI.Label(new Rect(cx - 100, cy + 30, 200, 30),
                      Bjorn.combo == 3 ? "¡REMATE!" : "COMBO x" + Bjorn.combo,
                      new GUIStyle(estiloHud) { alignment = TextAnchor.MiddleCenter });

        if (Bjorn.carga01 > 0.01f)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(cx - 60, cy + 60, 120, 12), texBlanca);
            GUI.color = Bjorn.carga01 >= 1f ? new Color(1f, 0.5f, 0.1f) : new Color(1f, 0.85f, 0.2f);
            GUI.DrawTexture(new Rect(cx - 58, cy + 62, 116 * Bjorn.carga01, 8), texBlanca);
            GUI.color = Color.white;
        }

        DibujarPoder(new Rect(20, Screen.height - 96, 165, 26), "SHIFT  Dash",
                     1f - Mathf.Clamp01(Bjorn.cdDashRestante / Bjorn.cooldownDash), true);
        DibujarPoder(new Rect(20, Screen.height - 64, 165, 26),
                     Bjorn.poderRayo ? "Q  Rayo Nv." + Bjorn.nivelRayo : "?  Runa perdida...",
                     Bjorn.poderRayo ? 1f - Mathf.Clamp01(Bjorn.cdRayoRestante / Bjorn.cooldownRayo) : 0f,
                     Bjorn.poderRayo);

        GUI.Label(new Rect(20, Screen.height - 32, 1200, 30),
                  "WASD + Raton  |  Clic: combo (manten: cargado)  |  Clic der: parry  |  SHIFT: dash  |  Q: rayo  |  E: poderes  |  TAB: habilidades",
                  estiloHud);

        if (Time.time < avisoGuardadoHasta)
            GUI.Label(new Rect(Screen.width - 240, 16, 220, 30), "Progreso guardado ✓", estiloHud);
    }

    void DibujarPanel()
    {
        float w = Mathf.Min(940f, Screen.width - 60f);
        float h = 460f;
        float x0 = (Screen.width - w) / 2f;
        float y0 = (Screen.height - h) / 2f;

        GUI.color = new Color(0.04f, 0.05f, 0.12f, 0.95f);
        GUI.DrawTexture(new Rect(x0, y0, w, h), texBlanca);
        GUI.color = Color.white;

        // pestañas
        if (GUI.Button(new Rect(x0 + 20, y0 + 12, 160, 30), pestanaPanel == 0 ? "> HABILIDADES <" : "HABILIDADES")) pestanaPanel = 0;
        if (GUI.Button(new Rect(x0 + 190, y0 + 12, 130, 30), pestanaPanel == 1 ? "> LOGROS <" : "LOGROS")) pestanaPanel = 1;
        if (GUI.Button(new Rect(x0 + 330, y0 + 12, 130, 30), pestanaPanel == 2 ? "> PODERES <" : "PODERES")) pestanaPanel = 2;
        GUI.Label(new Rect(x0 + 480, y0 + 14, w - 500, 26), "Almas: " + almas + "  (TAB/E: volver)", estiloHud);

        if (pestanaPanel == 0)
            DibujarHabilidades(x0, y0 + 50, w);
        else if (pestanaPanel == 1)
            DibujarLogros(x0, y0 + 50, w, h - 60);
        else
            DibujarPoderes(x0, y0 + 50, w);
    }

    void DibujarHabilidades(float x0, float y0, float w)
    {
        float colW = w / 3f;
        for (int r = 0; r < 3; r++)
        {
            float cx = x0 + r * colW;
            GUI.Label(new Rect(cx, y0, colW, 26), NombresRamas[r],
                      new GUIStyle(estiloHud) { alignment = TextAnchor.MiddleCenter });

            for (int c = 0; c < 3; c++)
            {
                Habilidad hab = habilidades[r][c];
                float hy = y0 + 34 + c * 106;

                GUI.color = new Color(1f, 1f, 1f, 0.08f);
                GUI.DrawTexture(new Rect(cx + 10, hy, colW - 20, 96), texBlanca);
                GUI.color = Color.white;

                string pips = "";
                for (int p = 0; p < hab.nivMax; p++) pips += p < hab.nivel ? "●" : "○";
                GUI.Label(new Rect(cx + 18, hy + 6, colW - 36, 22), hab.nombre + "  " + pips, estiloHud);
                GUI.Label(new Rect(cx + 18, hy + 30, colW - 36, 22), hab.desc,
                          new GUIStyle(estiloMision) { alignment = TextAnchor.UpperLeft });

                if (hab.nivel >= hab.nivMax)
                {
                    GUI.Label(new Rect(cx + 18, hy + 58, colW - 36, 26), "COMPLETA",
                              new GUIStyle(estiloHud) { normal = { textColor = new Color(0.4f, 1f, 0.6f) } });
                }
                else
                {
                    GUI.enabled = almas >= hab.Costo;
                    if (GUI.Button(new Rect(cx + 18, hy + 56, colW - 36, 30), "Mejorar  (" + hab.Costo + " almas)"))
                        ComprarHabilidad(r, c);
                    GUI.enabled = true;
                }
            }
        }
    }

    void DibujarLogros(float x0, float y0, float w, float h)
    {
        GUI.Label(new Rect(x0, y0, w, 26), "LOGROS: " + Logros.TotalDesbloqueados() + " / " + Logros.Lista.Length,
                  new GUIStyle(estiloHud) { alignment = TextAnchor.MiddleCenter });

        float colW = w / 2f;
        for (int i = 0; i < Logros.Lista.Length; i++)
        {
            var def = Logros.Lista[i];
            bool tiene = Logros.Tiene(def.id);
            float lx = x0 + (i < 10 ? 0 : colW) + 20;
            float ly = y0 + 34 + (i % 10) * 34;

            GUIStyle st = new GUIStyle(estiloMision) { alignment = TextAnchor.UpperLeft };
            st.normal.textColor = tiene ? new Color(1f, 0.85f, 0.3f) : new Color(0.5f, 0.55f, 0.65f);
            GUI.Label(new Rect(lx, ly, colW - 40, 32),
                      (tiene ? "[X] " : "[  ] ") + def.nombre + " - " + def.desc, st);
        }
    }

    /// <summary>Pestaña PODERES (tecla E): muestra que tienes y que falta desbloquear.</summary>
    void DibujarPoderes(float x0, float y0, float w)
    {
        string[][] poderes =
        {
            new[] { "Espada + Combo x3", "Clic: encadena 3 golpes, el tercero es un remate", "SI" },
            new[] { "Golpe Cargado",     "Manten el clic y suelta: giro de 360 grados",      "SI" },
            new[] { "Bloqueo y Parry",   "Clic derecho; en el momento justo aturde",         "SI" },
            new[] { "Dash",              "SHIFT: esquiva veloz con invulnerabilidad",        "SI" },
            new[] { "Doble Salto",       "ESPACIO en el aire salta otra vez",                "SI" },
            new[] { "Rayo de Odin",      Bjorn.poderRayo ? "Q: proyectil de trueno  -  Nivel " + Bjorn.nivelRayo + " de 3"
                                                         : "Encuentra la RUNA DEL TRUENO en el altar central",
                                         Bjorn.poderRayo ? "SI" : "NO" }
        };

        for (int i = 0; i < poderes.Length; i++)
        {
            float py = y0 + 14 + i * 58;
            bool tiene = poderes[i][2] == "SI";

            GUI.color = tiene ? new Color(1f, 0.85f, 0.2f, 0.12f) : new Color(1f, 1f, 1f, 0.05f);
            GUI.DrawTexture(new Rect(x0 + 30, py, w - 60, 50), texBlanca);
            GUI.color = Color.white;

            GUIStyle st = new GUIStyle(estiloHud);
            st.normal.textColor = tiene ? new Color(1f, 0.85f, 0.3f) : new Color(0.5f, 0.55f, 0.65f);
            GUI.Label(new Rect(x0 + 44, py + 4, 300, 24), (tiene ? "[X] " : "[?] ") + poderes[i][0], st);
            GUIStyle st2 = new GUIStyle(estiloMision) { alignment = TextAnchor.UpperLeft };
            st2.normal.textColor = st.normal.textColor;
            GUI.Label(new Rect(x0 + 44, py + 27, w - 100, 22), poderes[i][1], st2);
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
