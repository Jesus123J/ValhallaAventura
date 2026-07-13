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
    Texture2D texVineta;          // oscurecimiento de bordes de pantalla
    float marcadorGolpeHasta;     // marcador de impacto en la cruceta
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

        // viñeta: bordes de pantalla oscurecidos (mas cine, mas juego)
        const int NV = 128;
        texVineta = new Texture2D(NV, NV, TextureFormat.RGBA32, false);
        var pxV = new Color32[NV * NV];
        for (int y = 0; y < NV; y++)
            for (int x = 0; x < NV; x++)
            {
                float dx = (x - NV / 2f) / (NV / 2f);
                float dy = (y - NV / 2f) / (NV / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.SmoothStep(0f, 0.7f, (d - 0.55f) / 0.55f) * 255f);
                pxV[y * NV + x] = new Color32(0, 0, 0, a);
            }
        texVineta.SetPixels32(pxV);
        texVineta.Apply();

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

        // ---- camino de losetas antiguas hacia el portal ----
        for (int i = 0; i < 30; i++)
        {
            Transform loseta = ConstructorPersonaje.Cubo(null, "Loseta_" + i,
                new Vector3(-8f + i * 4f, 0.03f, Mathf.Sin(i * 0.9f) * 0.8f),
                new Vector3(2.0f, 0.08f, 1.5f), new Color(0.52f, 0.56f, 0.66f));
            loseta.rotation = Quaternion.Euler(0f, i * 13f % 20f - 10f, 0f);
        }

        // ---- estandartes vikingos rojos con emblema dorado ----
        float[,] estandartes = { {12,5}, {36,-6}, {62,6}, {88,-6} };
        for (int i = 0; i < estandartes.GetLength(0); i++)
        {
            Vector3 basePos = new Vector3(estandartes[i, 0], 0f, estandartes[i, 1]);
            CuboFisico("Poste_" + i, basePos + new Vector3(0f, 1.6f, 0f), new Vector3(0.14f, 3.2f, 0.14f), new Color(0.3f, 0.2f, 0.12f));
            ConstructorPersonaje.Cubo(null, "Bandera_" + i, basePos + new Vector3(0.42f, 2.5f, 0f), new Vector3(0.7f, 1.1f, 0.05f), new Color(0.62f, 0.12f, 0.1f));
            ConstructorPersonaje.Cubo(null, "Emblema_" + i, basePos + new Vector3(0.42f, 2.5f, -0.035f), new Vector3(0.3f, 0.3f, 0.02f), new Color(0.95f, 0.78f, 0.25f), true);
        }

        // ---- ruinas: arcos de piedra rotos ----
        float[,] ruinas = { {50, -12}, {78, 14} };
        for (int i = 0; i < ruinas.GetLength(0); i++)
        {
            Vector3 rp = new Vector3(ruinas[i, 0], 0f, ruinas[i, 1]);
            CuboFisico("RuinaIzq_" + i, rp + new Vector3(-1.6f, 1.5f, 0f), new Vector3(0.9f, 3f, 0.9f), new Color(0.38f, 0.41f, 0.5f));
            CuboFisico("RuinaDer_" + i, rp + new Vector3(1.6f, 1.0f, 0f), new Vector3(0.9f, 2f, 0.9f), new Color(0.38f, 0.41f, 0.5f));
            Transform dintelRoto = ConstructorPersonaje.Cubo(null, "RuinaDintel_" + i, rp + new Vector3(-0.4f, 3.1f, 0f), new Vector3(2.4f, 0.6f, 0.9f), new Color(0.34f, 0.37f, 0.45f));
            dintelRoto.rotation = Quaternion.Euler(0f, 0f, -12f);
        }

        // ---- monolitos runicos inclinados que brillan ----
        float[,] monolitos = { {44, 12}, {70, -12} };
        Color[] brillos = { new Color(0.3f, 0.9f, 1f), new Color(0.8f, 0.5f, 1f) };
        for (int i = 0; i < monolitos.GetLength(0); i++)
        {
            Transform mono = CuboFisico("Monolito_" + i, new Vector3(monolitos[i, 0], 1.7f, monolitos[i, 1]), new Vector3(1.2f, 3.6f, 0.7f), new Color(0.3f, 0.33f, 0.42f));
            mono.rotation = Quaternion.Euler(0f, 30f + i * 70f, i == 0 ? 8f : -6f);
            ConstructorPersonaje.Cubo(mono, "RunaMono1", new Vector3(0f, 0.15f, 0.52f), new Vector3(0.25f, 0.08f, 0.04f), brillos[i], true);
            ConstructorPersonaje.Cubo(mono, "RunaMono2", new Vector3(0f, 0.02f, 0.52f), new Vector3(0.08f, 0.2f, 0.04f), brillos[i], true);
            Light luzMono = mono.gameObject.AddComponent<Light>();
            luzMono.type = LightType.Point;
            luzMono.color = brillos[i];
            luzMono.range = 7f;
            luzMono.intensity = 1.8f;
        }

        // ---- huesos y craneos de batallas pasadas ----
        float[,] huesos = { {13,-8}, {27,9}, {49,4}, {66,10}, {83,7}, {98,-5} };
        for (int i = 0; i < huesos.GetLength(0); i++)
        {
            Vector3 hp = new Vector3(huesos[i, 0], 0.12f, huesos[i, 1]);
            Transform craneo = ConstructorPersonaje.Cubo(null, "Craneo_" + i, hp, new Vector3(0.28f, 0.24f, 0.3f), new Color(0.88f, 0.86f, 0.78f));
            craneo.rotation = Quaternion.Euler(0f, i * 57f, 0f);
            Transform huesoL = ConstructorPersonaje.Cubo(null, "Hueso_" + i, hp + new Vector3(0.4f, -0.06f, 0.2f), new Vector3(0.55f, 0.08f, 0.08f), new Color(0.88f, 0.86f, 0.78f));
            huesoL.rotation = Quaternion.Euler(0f, i * 33f + 20f, 0f);
        }

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
    /// <summary>Marca de impacto en la cruceta cuando tu espada conecta.</summary>
    public void MarcarGolpe() { marcadorGolpeHasta = Time.time + 0.14f; }

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

        // avisos rapidos: pergamino chico en la esquina inferior derecha
        if (Time.time < avisoEsquinaHasta && !tiendaAbierta && estado == Estado.Jugando)
        {
            float alfaAviso = Mathf.Clamp01((avisoEsquinaHasta - Time.time) / 0.5f);
            DibujarCajaPergamino(new Rect(Screen.width - 354f, Screen.height - 250f, 330f, 108f),
                                 avisoEsquina, alfaAviso, 15, null);
        }

        // dialogos de historia / fin: PERGAMINO GRANDE al lado derecho (nunca en el centro)
        if (!string.IsNullOrEmpty(mensajeCentral) && !tiendaAbierta)
        {
            float w = Mathf.Min(470f, Screen.width * 0.42f);
            float h = Mathf.Min(430f, Screen.height * 0.62f);
            DibujarCajaPergamino(new Rect(Screen.width - w - 26f, (Screen.height - h) * 0.5f, w, h),
                                 mensajeCentral, 1f, 17, "-  SAGA DEL NORTE  -");
        }
    }

    /// <summary>
    /// Caja de pergamino nordico: doble borde (madera + filo dorado), papel viejo,
    /// esquinas rasgadas, cenefa de rombos runicos y texto en tinta.
    /// </summary>
    void DibujarCajaPergamino(Rect r, string texto, float alfa, int tamanoLetra, string titulo)
    {
        // sombra proyectada
        GUI.color = new Color(0f, 0f, 0f, 0.35f * alfa);
        GUI.DrawTexture(new Rect(r.x + 6, r.y + 8, r.width, r.height), texBlanca);
        // borde de madera oscura
        GUI.color = new Color(0.28f, 0.18f, 0.09f, alfa);
        GUI.DrawTexture(new Rect(r.x - 6, r.y - 6, r.width + 12, r.height + 12), texBlanca);
        // filo dorado interior
        GUI.color = new Color(0.85f, 0.68f, 0.28f, alfa);
        GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), texBlanca);
        // papel viejo (dos tonos para dar textura)
        GUI.color = new Color(0.86f, 0.78f, 0.59f, alfa);
        GUI.DrawTexture(r, texBlanca);
        GUI.color = new Color(0.80f, 0.71f, 0.51f, alfa);
        GUI.DrawTexture(new Rect(r.x, r.y + r.height * 0.55f, r.width, r.height * 0.45f), texBlanca);
        // esquinas rasgadas
        GUI.color = new Color(0.28f, 0.18f, 0.09f, alfa);
        GUI.DrawTexture(new Rect(r.x, r.y, 16, 16), texBlanca);
        GUI.DrawTexture(new Rect(r.x + r.width - 16, r.y, 16, 16), texBlanca);
        GUI.DrawTexture(new Rect(r.x, r.y + r.height - 16, 16, 16), texBlanca);
        GUI.DrawTexture(new Rect(r.x + r.width - 16, r.y + r.height - 16, 16, 16), texBlanca);

        // cenefa de rombos runicos arriba y abajo
        GUI.color = new Color(0.5f, 0.34f, 0.14f, alfa);
        int rombos = Mathf.FloorToInt((r.width - 60f) / 26f);
        for (int i = 0; i < rombos; i++)
        {
            float rx = r.x + 34f + i * 26f;
            GUI.DrawTexture(new Rect(rx, r.y + 10f, 7, 7), texBlanca);
            GUI.DrawTexture(new Rect(rx, r.y + r.height - 17f, 7, 7), texBlanca);
        }

        float margenSup = 24f;
        if (!string.IsNullOrEmpty(titulo))
        {
            GUIStyle tituloSt = new GUIStyle(GUI.skin.label)
            { fontSize = 15, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            tituloSt.normal.textColor = new Color(0.45f, 0.12f, 0.08f, alfa); // tinta roja de saga
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 16, r.y + 20, r.width - 32, 24), titulo, tituloSt);
            GUI.color = new Color(0.5f, 0.34f, 0.14f, alfa);
            GUI.DrawTexture(new Rect(r.x + 40, r.y + 46, r.width - 80, 2), texBlanca);
            margenSup = 54f;
        }

        GUIStyle tinta = new GUIStyle(GUI.skin.label)
        { fontSize = tamanoLetra, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
        tinta.normal.textColor = new Color(0.24f, 0.14f, 0.05f, alfa);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 20, r.y + margenSup, r.width - 40, r.height - margenSup - 20f), texto, tinta);
    }

    /// <summary>Texto con sombra para que se lea sobre cualquier fondo.</summary>
    void LabelSombra(Rect r, string t, GUIStyle st)
    {
        GUIStyle sombra = new GUIStyle(st);
        sombra.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), t, sombra);
        GUI.Label(r, t, st);
    }

    void DibujarHUD()
    {
        float cx = Screen.width / 2f, cy = Screen.height / 2f;
        float pctVida = Mathf.Clamp01(Bjorn.vida / Bjorn.vidaMax);

        // ---- viñeta cinematografica + pulso rojo con vida baja ----
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texVineta);
        if (pctVida < 0.35f)
        {
            float pulso = (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f;
            GUI.color = new Color(0.9f, 0.05f, 0.05f, (0.35f - pctVida) * 1.6f * (0.4f + 0.6f * pulso));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texVineta);
            GUI.color = Color.white;
        }

        // ---- cruceta + marcador dorado cuando conectas un golpe ----
        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUI.DrawTexture(new Rect(cx - 9, cy - 1.5f, 18, 3), texBlanca);
        GUI.DrawTexture(new Rect(cx - 1.5f, cy - 9, 3, 18), texBlanca);
        if (Time.time < marcadorGolpeHasta)
        {
            GUI.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    GUI.DrawTexture(new Rect(cx + sx * 15 - 3, cy + sy * 15 - 3, 7, 7), texBlanca);
        }
        GUI.color = Color.white;

        // ---- BARRA DE VIDA grande con marco de madera ----
        Rect vr = new Rect(20, 16, 400, 42);
        GUI.color = new Color(0.28f, 0.18f, 0.09f, 0.95f);
        GUI.DrawTexture(new Rect(vr.x - 4, vr.y - 4, vr.width + 8, vr.height + 8), texBlanca);
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(vr, texBlanca);
        Color colVida = pctVida > 0.5f ? new Color(0.78f, 0.16f, 0.12f)
                       : pctVida > 0.25f ? new Color(0.95f, 0.5f, 0.1f)
                       : new Color(1f, 0.12f, 0.08f);
        GUI.color = colVida;
        GUI.DrawTexture(new Rect(vr.x + 4, vr.y + 4, (vr.width - 8) * pctVida, vr.height - 8), texBlanca);
        GUI.color = new Color(1f, 1f, 1f, 0.2f); // brillo superior
        GUI.DrawTexture(new Rect(vr.x + 4, vr.y + 4, (vr.width - 8) * pctVida, (vr.height - 8) * 0.4f), texBlanca);
        GUI.color = Color.white;
        LabelSombra(vr, "VIDA   " + Mathf.CeilToInt(Bjorn.vida) + " / " + Mathf.CeilToInt(Bjorn.vidaMax),
                    new GUIStyle(estiloHud) { fontSize = 20, alignment = TextAnchor.MiddleCenter });

        // ---- almas + racha, grandes y dorados ----
        GUIStyle almasSt = new GUIStyle(estiloHud) { fontSize = 20 };
        almasSt.normal.textColor = new Color(1f, 0.85f, 0.3f);
        string rachaTxt = Multiplicador > 1 ? "      RACHA x" + Multiplicador + " ¡!" : "";
        LabelSombra(new Rect(24, 66, 500, 30), "ALMAS  " + almas + rachaTxt, almasSt);

        // ---- objetivo con placa ----
        string objetivo;
        if (modoDesafio)
            objetivo = "OLEADA " + oleada + " / 5      PUNTOS: " + puntuacion;
        else
            objetivo = portalActivo ? "OBJETIVO: entra al portal de luz"
                                    : "OBJETIVO: acaba con los draugr   " + muertos + " / " + metaEnemigos;
        Rect or_ = new Rect(cx - 270, 14, 540, 36);
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(or_, texBlanca);
        GUI.color = new Color(0.85f, 0.68f, 0.28f, 0.9f);
        GUI.DrawTexture(new Rect(or_.x, or_.y + or_.height - 3, or_.width, 3), texBlanca);
        GUI.color = Color.white;
        LabelSombra(or_, objetivo, new GUIStyle(estiloHud) { fontSize = 18, alignment = TextAnchor.MiddleCenter });

        // ---- misiones con placa (solo historia) ----
        if (!modoDesafio)
        {
            float tiempo = Time.time - inicioNivel;
            Rect mr = new Rect(Screen.width - 336, 58, 316, 96);
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(mr, texBlanca);
            GUI.color = new Color(0.85f, 0.68f, 0.28f, 0.9f);
            GUI.DrawTexture(new Rect(mr.x, mr.y, 3, mr.height), texBlanca);
            GUI.color = Color.white;
            GUI.Label(new Rect(mr.x + 12, mr.y + 6, mr.width - 20, mr.height - 12),
                      (sinDano ? "O" : "X") + " Sin recibir dano\n" +
                      "O Runas doradas " + almasRecogidas + "/14\n" +
                      (tiempo <= TiempoMision ? "O" : "X") + " Tiempo " + Reloj(tiempo) + " / 5:00",
                      new GUIStyle(estiloMision) { alignment = TextAnchor.UpperLeft, fontSize = 15 });
        }

        // ---- combo grande ----
        if (Time.time < Bjorn.comboHasta && Bjorn.combo > 1)
        {
            GUIStyle comboSt = new GUIStyle(estiloHud) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            comboSt.normal.textColor = Bjorn.combo == 3 ? new Color(1f, 0.5f, 0.1f) : new Color(1f, 0.85f, 0.3f);
            LabelSombra(new Rect(cx - 150, cy + 34, 300, 36),
                        Bjorn.combo == 3 ? "¡ REMATE !" : "COMBO x" + Bjorn.combo, comboSt);
        }

        // ---- barra de GOLPE CARGADO grande ----
        if (Bjorn.carga01 > 0.01f)
        {
            Rect cr = new Rect(cx - 130, cy + 78, 260, 24);
            GUI.color = new Color(0.28f, 0.18f, 0.09f, 0.95f);
            GUI.DrawTexture(new Rect(cr.x - 3, cr.y - 3, cr.width + 6, cr.height + 6), texBlanca);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(cr, texBlanca);
            GUI.color = Bjorn.carga01 >= 1f ? new Color(1f, 0.45f, 0.05f) : new Color(1f, 0.85f, 0.2f);
            GUI.DrawTexture(new Rect(cr.x + 3, cr.y + 3, (cr.width - 6) * Bjorn.carga01, cr.height - 6), texBlanca);
            GUI.color = Color.white;
            LabelSombra(new Rect(cr.x, cr.y - 26, cr.width, 24),
                        Bjorn.carga01 >= 1f ? "¡ SUELTA !" : "GOLPE CARGADO...",
                        new GUIStyle(estiloHud) { fontSize = 17, alignment = TextAnchor.MiddleCenter });
        }

        // ---- poderes grandes ----
        DibujarPoder(new Rect(20, Screen.height - 122, 230, 36), "SHIFT   DASH",
                     1f - Mathf.Clamp01(Bjorn.cdDashRestante / Bjorn.cooldownDash), true);
        DibujarPoder(new Rect(20, Screen.height - 80, 230, 36),
                     Bjorn.poderRayo ? "Q   RAYO  Nv." + Bjorn.nivelRayo : "?   Runa perdida...",
                     Bjorn.poderRayo ? 1f - Mathf.Clamp01(Bjorn.cdRayoRestante / Bjorn.cooldownRayo) : 0f,
                     Bjorn.poderRayo);

        if (Bjorn.agachado)
            LabelSombra(new Rect(270, Screen.height - 74, 220, 30), "AGACHADO",
                        new GUIStyle(estiloHud) { fontSize = 18 });

        LabelSombra(new Rect(20, Screen.height - 32, 1300, 30),
                  "WASD + Raton  |  Clic: combo (manten: cargado)  |  Clic der: parry  |  Ctrl: agacharse  |  SHIFT: dash  |  Q: rayo  |  E: poderes  |  TAB: habilidades",
                  new GUIStyle(estiloHud) { fontSize = 14 });

        if (Time.time < avisoGuardadoHasta)
            LabelSombra(new Rect(Screen.width - 250, 16, 230, 30), "Progreso guardado ✓", estiloHud);
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
        if (GUI.Button(new Rect(x0 + 20, y0 + 12, 150, 30), pestanaPanel == 0 ? "> HABILIDADES <" : "HABILIDADES")) pestanaPanel = 0;
        if (GUI.Button(new Rect(x0 + 180, y0 + 12, 115, 30), pestanaPanel == 1 ? "> LOGROS <" : "LOGROS")) pestanaPanel = 1;
        if (GUI.Button(new Rect(x0 + 305, y0 + 12, 115, 30), pestanaPanel == 2 ? "> PODERES <" : "PODERES")) pestanaPanel = 2;
        if (GUI.Button(new Rect(x0 + 430, y0 + 12, 105, 30), pestanaPanel == 3 ? "> FORJA <" : "FORJA")) pestanaPanel = 3;
        GUI.Label(new Rect(x0 + 555, y0 + 14, w - 575, 26), "Almas: " + almas + "  (TAB: volver)", estiloHud);

        if (pestanaPanel == 0)
            DibujarHabilidades(x0, y0 + 50, w);
        else if (pestanaPanel == 1)
            DibujarLogros(x0, y0 + 50, w, h - 60);
        else if (pestanaPanel == 2)
            DibujarPoderes(x0, y0 + 50, w);
        else
            DibujarForja(x0, y0 + 50, w);
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

    /// <summary>
    /// FORJA: el jugador CREA su propia espada eligiendo colores de empuñadura,
    /// hoja y guante. Se aplica al instante (la ves en tu mano) y queda guardada.
    /// </summary>
    void DibujarForja(float x0, float y0, float w)
    {
        GUI.Label(new Rect(x0, y0 + 4, w, 26), "FORJA DEL HERRERO - crea tu propia espada",
                  new GUIStyle(estiloHud) { alignment = TextAnchor.MiddleCenter });

        string[] categorias = { "EMPUNADURA Y POMO", "HOJA", "GUANTE" };
        Color[][] paletas = { Jugador.ColoresEmpunadura, Jugador.ColoresHoja, Jugador.ColoresGuante };

        for (int c = 0; c < 3; c++)
        {
            float fy = y0 + 44 + c * 74;
            GUI.Label(new Rect(x0 + 40, fy, 260, 24), categorias[c], estiloHud);
            int actual = PlayerPrefs.GetInt("forja_" + c, 0);

            for (int i = 0; i < paletas[c].Length; i++)
            {
                Rect br = new Rect(x0 + 40 + i * 66, fy + 26, 56, 34);
                // marco dorado en la opcion elegida
                if (i == actual)
                {
                    GUI.color = new Color(1f, 0.85f, 0.2f);
                    GUI.DrawTexture(new Rect(br.x - 3, br.y - 3, br.width + 6, br.height + 6), texBlanca);
                }
                GUI.color = paletas[c][i];
                GUI.DrawTexture(br, texBlanca);
                GUI.color = Color.white;
                if (GUI.Button(br, "", GUIStyle.none) && i != actual)
                {
                    PlayerPrefs.SetInt("forja_" + c, i);
                    PlayerPrefs.Save();
                    Bjorn.ReconstruirEspada(); // se ve al instante en tu mano
                    sfxSrc.PlayOneShot(sfxImpacto); // martillazo de herrero
                }
            }
        }

        // vista previa: la espada dibujada con tus colores
        float px = x0 + w - 300, py = y0 + 60;
        GUI.Label(new Rect(px, py - 26, 260, 24), "Asi se ve:", estiloHud);
        Color emp = Jugador.ColoresEmpunadura[PlayerPrefs.GetInt("forja_0", 0)];
        Color hoja = Jugador.ColoresHoja[PlayerPrefs.GetInt("forja_1", 0)];
        GUI.color = emp;   GUI.DrawTexture(new Rect(px, py + 60, 26, 26), texBlanca);           // pomo
        GUI.color = new Color(0.35f, 0.25f, 0.15f); GUI.DrawTexture(new Rect(px + 26, py + 66, 40, 14), texBlanca); // mango
        GUI.color = emp;   GUI.DrawTexture(new Rect(px + 66, py + 48, 14, 50), texBlanca);      // guarda
        GUI.color = hoja;  GUI.DrawTexture(new Rect(px + 80, py + 62, 150, 22), texBlanca);     // hoja
        GUI.color = hoja;  GUI.DrawTexture(new Rect(px + 230, py + 66, 22, 14), texBlanca);     // punta
        GUI.color = new Color(1f, 1f, 1f, 0.7f); GUI.DrawTexture(new Rect(px + 82, py + 64, 146, 4), texBlanca); // filo
        GUI.color = Color.white;
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
