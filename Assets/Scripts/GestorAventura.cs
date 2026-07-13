using UnityEngine;
using System.Collections;

/// <summary>
/// Director del MUNDO ABIERTO "Camino al Valhalla":
/// - Mundo voxel INFINITO estilo Minecraft con semilla propia guardada:
///   viaja entre biomas (tundra, bosque, pradera, ceniza, montanas) SIN puertas.
/// - Ciclo de DIA y NOCHE: de noche salen mas draugr (y algun guardian).
/// - Los draugr aparecen alrededor mientras exploras; las almas, cofres,
///   hogueras y la Runa del Trueno estan repartidos por el mundo.
/// - Se conserva todo lo demas: tienda-arbol, logros, forja, racha, pergaminos,
///   dificultad adaptativa y guardado automatico.
/// </summary>
public class GestorAventura : MonoBehaviour
{
    public static GestorAventura Instancia;
    public Jugador Bjorn { get; private set; }

    // mundo y ciclo de dia
    MundoInfinito mundo;
    Transform cielo;            // aurora + luna que siguen al jugador
    Light sol;
    const float DuracionDia = 360f; // segundos por dia completo
    float horaDia = 0.3f;           // 0..1 (0.25 = amanecer)
    bool EsNoche { get { return horaDia > 0.72f || horaDia < 0.22f; } }

    // spawner de enemigos
    float proxSpawn;

    // ================= SENDA DEL EINHERJAR (progresion guiada) =================
    enum EventoSenda { Hoguera, Matar, Bioma, Cofre, Runa, Noche, Jefe, AlmasTotal, Comprar }
    class PasoSenda
    {
        public string desc;
        public EventoSenda evento;
        public int meta;
        public int recompensa;
        public PasoSenda(string d, EventoSenda e, int m, int r) { desc = d; evento = e; meta = m; recompensa = r; }
    }
    static readonly PasoSenda[] Senda =
    {
        new PasoSenda("Enciende tu primera hoguera",       EventoSenda.Hoguera,    1,  100),
        new PasoSenda("Derrota 5 draugr",                  EventoSenda.Matar,      5,  150),
        new PasoSenda("Visita 2 biomas distintos",         EventoSenda.Bioma,      2,  150),
        new PasoSenda("Abre un cofre del tesoro",          EventoSenda.Cofre,      1,  150),
        new PasoSenda("Encuentra la Runa del Trueno",      EventoSenda.Runa,       1,  200),
        new PasoSenda("Sobrevive una noche entera",        EventoSenda.Noche,      1,  250),
        new PasoSenda("Derrota a un GUARDIAN errante",     EventoSenda.Jefe,       1,  300),
        new PasoSenda("Reune 1500 almas",                  EventoSenda.AlmasTotal, 1500, 400),
        new PasoSenda("Compra 3 habilidades",              EventoSenda.Comprar,    3,  400),
        new PasoSenda("Derrota 25 draugr",                 EventoSenda.Matar,      25, 500)
    };
    int pasoSenda;       // en cual paso vas (persistente)
    int progSenda;       // progreso dentro del paso (persistente)
    bool enNochePrev;
    bool muertoEstaNoche;

    int almas;
    int racha;
    int Multiplicador { get { return 1 + Mathf.Min(2, racha / 2); } }
    int muertesSesion;

    enum Estado { Intro, Jugando }
    Estado estado = Estado.Intro;
    string mensajeCentral = "";
    string avisoEsquina = "";
    float avisoEsquinaHasta;
    float avisoGuardadoHasta;
    bool tiendaAbierta;
    int pestanaPanel;

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
    Texture2D texBlanca, texVineta;
    float marcadorGolpeHasta;
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
        CrearVineta();

        // ---- semilla del mundo: unica por jugador, para siempre ----
        if (!PlayerPrefs.HasKey("semilla"))
            PlayerPrefs.SetInt("semilla", System.Environment.TickCount & 0x7FFFFFFF);
        GeneradorMundo.Iniciar(PlayerPrefs.GetInt("semilla"));

        DefinirHabilidades();
        ConstruirMundo();
        Hoguera.PuntoRespawn = Bjorn.transform.position;
        Enemigo.FactorDanoGlobal = 1f;
        almas = PlayerPrefs.GetInt("almas", 0);
        Bjorn.poderRayo = PlayerPrefs.GetInt("poder_rayo", 0) == 1;
        pasoSenda = PlayerPrefs.GetInt("senda_paso", 0);
        progSenda = PlayerPrefs.GetInt("senda_prog", 0);
        CargarHabilidades();
        AplicarHabilidades();
        StartCoroutine(Intro());
    }

    void CrearVineta()
    {
        const int NV = 128;
        texVineta = new Texture2D(NV, NV, TextureFormat.RGBA32, false);
        var px = new Color32[NV * NV];
        for (int y = 0; y < NV; y++)
            for (int x = 0; x < NV; x++)
            {
                float dx = (x - NV / 2f) / (NV / 2f);
                float dy = (y - NV / 2f) / (NV / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(Mathf.SmoothStep(0f, 0.7f, (d - 0.55f) / 0.55f) * 255f);
                px[y * NV + x] = new Color32(0, 0, 0, a);
            }
        texVineta.SetPixels32(px);
        texVineta.Apply();
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
        RegistrarSenda(EventoSenda.Comprar);
        bool todo = true;
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                if (habilidades[r][c].nivel < habilidades[r][c].nivMax) todo = false;
        if (todo) Logros.Desbloquear("arbol");
    }

    // ============================================================
    //  MUNDO ABIERTO
    // ============================================================
    void ConstruirMundo()
    {
        Camera cam = Camera.main;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 260f;
        cam.clearFlags = CameraClearFlags.SolidColor;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.016f;

        sol = FindObjectOfType<Light>();
        if (sol != null)
        {
            sol.type = LightType.Directional;
            sol.shadows = LightShadows.Soft;
        }

        // jugador nace sobre el terreno del origen
        int hInicio = GeneradorMundo.Altura(0, 0);
        GameObject bjornGO = new GameObject("Bjorn");
        bjornGO.transform.position = new Vector3(0.5f, hInicio + 2.5f, 0.5f);
        Bjorn = bjornGO.AddComponent<Jugador>();

        // mundo infinito por chunks
        mundo = new GameObject("MundoInfinito").AddComponent<MundoInfinito>();
        mundo.jugador = bjornGO.transform;
        mundo.GenerarInmediato(bjornGO.transform.position, 2); // suelo bajo los pies desde el frame 1

        // cielo que viaja contigo: aurora boreal + luna, siempre al norte
        cielo = new GameObject("Cielo").transform;
        Color[] coloresAurora =
        {
            new Color(0.15f, 0.9f, 0.55f, 0.30f),
            new Color(0.3f, 0.75f, 1f, 0.26f),
            new Color(0.7f, 0.4f, 1f, 0.24f)
        };
        for (int i = 0; i < 3; i++)
        {
            Transform banda = ConstructorPersonaje.Rect(cielo, "Aurora_" + i,
                Vector2.zero, new Vector2(400f, 7f), coloresAurora[i], 0, 1f, true);
            banda.localPosition = new Vector3(0f, 55f + i * 7f, 130f);
            banda.localRotation = Quaternion.Euler(12f, 0f, -3f + i * 3f);
        }
        Transform halo = ConstructorPersonaje.Circ(cielo, "HaloLuna", Vector2.zero, 16f, new Color(0.9f, 0.95f, 1f, 0.18f), 0, true);
        halo.localPosition = new Vector3(-35f, 52f, 120f);
        Transform luna = ConstructorPersonaje.Circ(cielo, "Luna", Vector2.zero, 10f, new Color(0.95f, 0.97f, 1f), 0, true);
        luna.localPosition = new Vector3(-35f, 52f, 120f);

        // nieve ambiental (se activa solo en biomas frios)
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
    //  DIALOGOS (pergamino, letra a letra, sin voz)
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

        yield return MostrarDialogo(
            "CAMINO AL VALHALLA\n\nMUNDO ABIERTO\n\nSemilla de tu mundo: " + GeneradorMundo.Semilla +
            (almas > 0 ? "\nAlmas: " + almas : "") +
            "\nLogros: " + Logros.TotalDesbloqueados() + "/20");

        yield return MostrarDialogo(
            "Los nueve reinos se han fundido\nen un solo mundo sin puertas.\n\nCamina y los descubriras: la tundra helada,\nlos bosques de pinos, las praderas,\nlas tierras de ceniza y las montanas grises.");

        yield return MostrarDialogo(
            "Los draugr acechan... y de NOCHE son mas.\n\nSigue la SENDA DEL EINHERJAR\n(arriba a la derecha): 10 pasos que te\nguian y te premian con almas." +
            (pasoSenda < Senda.Length ? "\n\nTu primer paso:\n" + Senda[pasoSenda].desc : "") +
            "\n\nTAB: habilidades y forja | E: poderes");

        estado = Estado.Jugando;
        Bjorn.controlActivo = true;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // ============================================================
    //  EVENTOS
    // ============================================================
    /// <summary>Avanza la Senda del Einherjar cuando ocurre el evento del paso actual.</summary>
    void RegistrarSenda(EventoSenda evento, int cantidad = 1, int valorAbsoluto = -1)
    {
        if (pasoSenda >= Senda.Length) return;
        PasoSenda paso = Senda[pasoSenda];
        if (paso.evento != evento) return;

        progSenda = valorAbsoluto >= 0 ? valorAbsoluto : progSenda + cantidad;
        PlayerPrefs.SetInt("senda_prog", progSenda);

        if (progSenda >= paso.meta)
        {
            almas += paso.recompensa;
            pasoSenda++;
            progSenda = 0;
            PlayerPrefs.SetInt("senda_paso", pasoSenda);
            PlayerPrefs.SetInt("senda_prog", 0);
            Guardar();
            sfxSrc.PlayOneShot(sfxFanfarria);

            string siguiente = pasoSenda < Senda.Length
                ? "Siguiente: " + Senda[pasoSenda].desc
                : "¡HAS COMPLETADO LA SENDA!\nEres un verdadero EINHERJAR";
            StartCoroutine(MensajeTemporal("SENDA: ¡paso completado!  +" + paso.recompensa + " almas\n" + siguiente, 4f));
        }
        else
        {
            PlayerPrefs.Save();
        }
    }

    public void MarcarGolpe() { marcadorGolpeHasta = Time.time + 0.14f; }
    public void SonidoEspada() { sfxSrc.PlayOneShot(sfxEspada); }
    public void SonidoImpacto() { sfxSrc.PlayOneShot(sfxImpacto); }
    public void SonidoTrueno() { sfxSrc.PlayOneShot(sfxTrueno); }
    public void SonidoRemate() { sfxSrc.PlayOneShot(sfxRemate); }
    public void SonidoParry() { sfxSrc.PlayOneShot(sfxEspada); sfxSrc.PlayOneShot(sfxImpacto); }

    public void MostrarAviso(string msg)
    {
        avisoEsquina = msg;
        avisoEsquinaHasta = Time.time + 2.2f;
    }

    IEnumerator MensajeTemporal(string msg, float dur)
    {
        avisoEsquina = msg;
        avisoEsquinaHasta = Time.time + dur;
        yield break;
    }

    public void LogroDesbloqueado(string nombre)
    {
        sfxSrc.PlayOneShot(sfxFanfarria);
        StartCoroutine(MensajeTemporal("LOGRO DESBLOQUEADO\n" + nombre, 2.2f));
    }

    public void HogueraEncendida()
    {
        sfxSrc.PlayOneShot(sfxFanfarria);
        MostrarAviso("Hoguera encendida: vida restaurada\ny punto de descanso guardado");
        RegistrarSenda(EventoSenda.Hoguera);
    }

    public void JugadorHerido()
    {
        sfxSrc.PlayOneShot(sfxHerido);
        if (racha > 0) MostrarAviso("Racha perdida...");
        racha = 0;
    }

    public void Recoger(Recompensa r)
    {
        switch (r.tipo)
        {
            case Recompensa.Tipo.Almas:
                GanarAlmas(25);
                break;
            case Recompensa.Tipo.Pocion:
                Bjorn.vida = Mathf.Min(Bjorn.vidaMax, Bjorn.vida + 30f);
                sfxSrc.PlayOneShot(sfxAlmas);
                MostrarAviso("+30 DE VIDA");
                break;
            case Recompensa.Tipo.RunaTrueno:
                Bjorn.poderRayo = true;
                PlayerPrefs.SetInt("poder_rayo", 1);
                PlayerPrefs.Save();
                sfxSrc.PlayOneShot(sfxFanfarria);
                StartCoroutine(MensajeTemporal("¡PODER DESBLOQUEADO!\nRAYO DE ODIN - pulsa Q", 3.5f));
                RegistrarSenda(EventoSenda.Runa);
                break;
        }
    }

    void GanarAlmas(int base_)
    {
        int ganadas = base_ * Multiplicador;
        almas += ganadas;
        sfxSrc.pitch = 1f + (Multiplicador - 1) * 0.15f;
        sfxSrc.PlayOneShot(sfxAlmas);
        sfxSrc.pitch = 1f;
        Guardar();

        int total = Logros.Contar("almas", ganadas);
        if (total >= 1000) Logros.Desbloquear("rico");
        if (total >= 3000) Logros.Desbloquear("millonario");
        RegistrarSenda(EventoSenda.AlmasTotal, 0, almas);
    }

    public void CofreAbierto(string msg)
    {
        sfxSrc.PlayOneShot(sfxFanfarria);
        MostrarAviso(msg);
        if (Logros.Contar("cofres", 1) >= 3) Logros.Desbloquear("abridor");
        RegistrarSenda(EventoSenda.Cofre);
    }

    public void CofreGema()
    {
        sfxSrc.PlayOneShot(sfxFanfarria);
        GanarAlmas(150);
        MostrarAviso("¡GEMA RARA! +150 almas");
        if (Logros.Contar("cofres", 1) >= 3) Logros.Desbloquear("abridor");
        RegistrarSenda(EventoSenda.Cofre);
    }

    public void EnemigoMuerto(Enemigo e)
    {
        racha++;
        GanarAlmas(e.esJefe ? 500 : 100);

        int total = Logros.Contar("draugr", 1);
        if (total >= 1) Logros.Desbloquear("primera_sangre");
        if (total >= 10) Logros.Desbloquear("cazador");
        if (total >= 50) Logros.Desbloquear("exterminador");
        if (e.esJefe)
        {
            Logros.Desbloquear("jefe");
            MostrarAviso("¡GUARDIAN DERROTADO!  +500 almas");
            RegistrarSenda(EventoSenda.Jefe);
        }
        RegistrarSenda(EventoSenda.Matar);
    }

    public void JugadorCaido()
    {
        muertoEstaNoche = true; // esta noche ya no cuenta como "sobrevivida"
        StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        Bjorn.controlActivo = false;
        mensajeCentral = "HAS CAIDO...\n\npero un einherjar siempre se levanta.";

        muertesSesion++;
        if (muertesSesion >= 2)
        {
            Enemigo.FactorDanoGlobal = 0.85f; // dificultad adaptativa invisible
            Recompensa.Crear(Recompensa.Tipo.Pocion, Hoguera.PuntoRespawn + new Vector3(1.5f, 0.5f, 1f));
        }

        yield return new WaitForSeconds(2.5f);
        mundo.GenerarInmediato(Hoguera.PuntoRespawn, 2);
        Bjorn.Teletransportar(Hoguera.PuntoRespawn + Vector3.up * 1.5f);
        mensajeCentral = "";
        Bjorn.controlActivo = true;
    }

    void Guardar()
    {
        PlayerPrefs.SetInt("almas", almas);
        PlayerPrefs.Save();
        avisoGuardadoHasta = Time.time + 2f;
    }

    // ============================================================
    //  BUCLE: dia/noche + spawner de draugr
    // ============================================================
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Cursor.lockState = CursorLockMode.None;
        if (estado == Estado.Jugando && !tiendaAbierta && Input.GetMouseButtonDown(0) &&
            Cursor.lockState != CursorLockMode.Locked)
            Cursor.lockState = CursorLockMode.Locked;

        if (estado == Estado.Jugando && (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.E)))
        {
            if (!tiendaAbierta)
                pestanaPanel = Input.GetKeyDown(KeyCode.E) ? 2 : 0;
            tiendaAbierta = !tiendaAbierta;
            Bjorn.controlActivo = !tiendaAbierta;
            Cursor.lockState = tiendaAbierta ? CursorLockMode.None : CursorLockMode.Locked;
        }

        ActualizarCiclodia();

        if (estado != Estado.Jugando || Bjorn == null) return;

        // el cielo viaja contigo
        if (cielo != null)
            cielo.position = new Vector3(Bjorn.transform.position.x, 0f, Bjorn.transform.position.z);

        // senda: registrar biomas visitados y noches sobrevividas
        int biomaBit = 1 << (int)GeneradorMundo.Bioma(
            Mathf.FloorToInt(Bjorn.transform.position.x), Mathf.FloorToInt(Bjorn.transform.position.z));
        int visitados = PlayerPrefs.GetInt("biomas_visitados", 0);
        if ((visitados & biomaBit) == 0)
        {
            visitados |= biomaBit;
            PlayerPrefs.SetInt("biomas_visitados", visitados);
            int cuantos = 0;
            for (int b = 0; b < 5; b++) if ((visitados & (1 << b)) != 0) cuantos++;
            RegistrarSenda(EventoSenda.Bioma, 0, cuantos);
        }
        if (EsNoche && !enNochePrev) muertoEstaNoche = false;       // empieza la noche
        if (!EsNoche && enNochePrev && !muertoEstaNoche)            // amanecio y seguiste en pie
            RegistrarSenda(EventoSenda.Noche);
        enNochePrev = EsNoche;

        // spawner de draugr alrededor (mas de noche)
        if (Time.time > proxSpawn && !tiendaAbierta)
        {
            proxSpawn = Time.time + 4f;
            int vivos = 0;
            foreach (Enemigo e in Enemigo.Todos)
                if (e != null && !e.muerto) vivos++;

            int maximo = EsNoche ? 9 : 4;
            if (vivos < maximo)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(20f, 32f);
                Vector3 p = Bjorn.transform.position + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                p.y = GeneradorMundo.Altura(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
                bool jefe = EsNoche && Random.value < 0.07f; // guardianes errantes de noche
                CrearDraugr(p, jefe);
            }

            // despawn de los que quedaron muy lejos
            foreach (Enemigo e in Enemigo.Todos.ToArray())
                if (e != null && !e.muerto &&
                    (e.transform.position - Bjorn.transform.position).magnitude > 55f)
                    Destroy(e.gameObject);
        }
    }

    void ActualizarCiclodia()
    {
        horaDia = (horaDia + Time.deltaTime / DuracionDia) % 1f;
        float d = Mathf.Clamp01(Mathf.Sin(horaDia * Mathf.PI * 2f - Mathf.PI * 0.5f) * 1.4f + 0.5f); // 0 noche, 1 dia

        if (sol != null)
        {
            sol.transform.rotation = Quaternion.Euler(horaDia * 360f - 90f, -35f, 0f);
            sol.intensity = Mathf.Lerp(0.06f, 1.05f, d);
            sol.color = Color.Lerp(new Color(0.55f, 0.6f, 0.9f), new Color(1f, 0.96f, 0.88f), d);
        }
        RenderSettings.ambientLight = Color.Lerp(new Color(0.10f, 0.11f, 0.2f), new Color(0.45f, 0.48f, 0.55f), d);
        RenderSettings.fogColor = Color.Lerp(new Color(0.04f, 0.05f, 0.11f), new Color(0.55f, 0.66f, 0.82f), d);
        if (Camera.main != null)
            Camera.main.backgroundColor = Color.Lerp(new Color(0.02f, 0.03f, 0.09f), new Color(0.45f, 0.65f, 0.9f), d);
    }

    string HoraTexto()
    {
        int hh = Mathf.FloorToInt(horaDia * 24f);
        int mm = Mathf.FloorToInt((horaDia * 24f - hh) * 60f);
        return hh.ToString("00") + ":" + mm.ToString("00");
    }

    // ============================================================
    //  HUD Y PANELES
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

        if (Time.time < avisoEsquinaHasta && !tiendaAbierta && estado == Estado.Jugando)
        {
            float alfaAviso = Mathf.Clamp01((avisoEsquinaHasta - Time.time) / 0.5f);
            DibujarCajaPergamino(new Rect(Screen.width - 354f, Screen.height - 250f, 330f, 108f),
                                 avisoEsquina, alfaAviso, 15, null);
        }

        if (!string.IsNullOrEmpty(mensajeCentral) && !tiendaAbierta)
        {
            float w = Mathf.Min(470f, Screen.width * 0.42f);
            float h = Mathf.Min(430f, Screen.height * 0.62f);
            DibujarCajaPergamino(new Rect(Screen.width - w - 26f, (Screen.height - h) * 0.5f, w, h),
                                 mensajeCentral, 1f, 17, "-  SAGA DEL NORTE  -");
        }
    }

    void DibujarCajaPergamino(Rect r, string texto, float alfa, int tamanoLetra, string titulo)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.35f * alfa);
        GUI.DrawTexture(new Rect(r.x + 6, r.y + 8, r.width, r.height), texBlanca);
        GUI.color = new Color(0.28f, 0.18f, 0.09f, alfa);
        GUI.DrawTexture(new Rect(r.x - 6, r.y - 6, r.width + 12, r.height + 12), texBlanca);
        GUI.color = new Color(0.85f, 0.68f, 0.28f, alfa);
        GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), texBlanca);
        GUI.color = new Color(0.86f, 0.78f, 0.59f, alfa);
        GUI.DrawTexture(r, texBlanca);
        GUI.color = new Color(0.80f, 0.71f, 0.51f, alfa);
        GUI.DrawTexture(new Rect(r.x, r.y + r.height * 0.55f, r.width, r.height * 0.45f), texBlanca);
        GUI.color = new Color(0.28f, 0.18f, 0.09f, alfa);
        GUI.DrawTexture(new Rect(r.x, r.y, 16, 16), texBlanca);
        GUI.DrawTexture(new Rect(r.x + r.width - 16, r.y, 16, 16), texBlanca);
        GUI.DrawTexture(new Rect(r.x, r.y + r.height - 16, 16, 16), texBlanca);
        GUI.DrawTexture(new Rect(r.x + r.width - 16, r.y + r.height - 16, 16, 16), texBlanca);

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
            tituloSt.normal.textColor = new Color(0.45f, 0.12f, 0.08f, alfa);
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

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texVineta);
        if (pctVida < 0.35f)
        {
            float pulso = (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f;
            GUI.color = new Color(0.9f, 0.05f, 0.05f, (0.35f - pctVida) * 1.6f * (0.4f + 0.6f * pulso));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texVineta);
            GUI.color = Color.white;
        }

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

        // barra de vida grande
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
        GUI.color = new Color(1f, 1f, 1f, 0.2f);
        GUI.DrawTexture(new Rect(vr.x + 4, vr.y + 4, (vr.width - 8) * pctVida, (vr.height - 8) * 0.4f), texBlanca);
        GUI.color = Color.white;
        LabelSombra(vr, "VIDA   " + Mathf.CeilToInt(Bjorn.vida) + " / " + Mathf.CeilToInt(Bjorn.vidaMax),
                    new GUIStyle(estiloHud) { fontSize = 20, alignment = TextAnchor.MiddleCenter });

        GUIStyle almasSt = new GUIStyle(estiloHud) { fontSize = 20 };
        almasSt.normal.textColor = new Color(1f, 0.85f, 0.3f);
        string rachaTxt = Multiplicador > 1 ? "      RACHA x" + Multiplicador + " ¡!" : "";
        LabelSombra(new Rect(24, 66, 500, 30), "ALMAS  " + almas + rachaTxt, almasSt);

        // placa: bioma actual + hora del dia
        GeneradorMundo.TipoBioma bioma = GeneradorMundo.Bioma(
            Mathf.FloorToInt(Bjorn.transform.position.x), Mathf.FloorToInt(Bjorn.transform.position.z));
        string lugar = GeneradorMundo.NombreBioma(bioma) + "     " +
                       (EsNoche ? "NOCHE " : "DIA ") + HoraTexto();
        Rect or_ = new Rect(cx - 270, 14, 540, 36);
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(or_, texBlanca);
        GUI.color = new Color(0.85f, 0.68f, 0.28f, 0.9f);
        GUI.DrawTexture(new Rect(or_.x, or_.y + or_.height - 3, or_.width, 3), texBlanca);
        GUI.color = Color.white;
        LabelSombra(or_, lugar, new GUIStyle(estiloHud) { fontSize = 18, alignment = TextAnchor.MiddleCenter });

        // ---- SENDA DEL EINHERJAR: tu siguiente paso, siempre visible ----
        Rect sr = new Rect(Screen.width - 356, 56, 336, 64);
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(sr, texBlanca);
        GUI.color = new Color(0.85f, 0.68f, 0.28f, 0.9f);
        GUI.DrawTexture(new Rect(sr.x, sr.y, 3, sr.height), texBlanca);
        GUI.color = Color.white;
        if (pasoSenda < Senda.Length)
        {
            PasoSenda paso = Senda[pasoSenda];
            string prog = paso.meta > 1 ? "   (" + Mathf.Min(progSenda, paso.meta) + "/" + paso.meta + ")" : "";
            GUIStyle tituloSenda = new GUIStyle(estiloMision) { alignment = TextAnchor.UpperLeft, fontSize = 13 };
            tituloSenda.normal.textColor = new Color(1f, 0.85f, 0.3f);
            GUI.Label(new Rect(sr.x + 12, sr.y + 6, sr.width - 20, 20),
                      "SENDA DEL EINHERJAR  " + (pasoSenda + 1) + "/" + Senda.Length, tituloSenda);
            GUI.Label(new Rect(sr.x + 12, sr.y + 28, sr.width - 20, 30),
                      paso.desc + prog + "   +" + paso.recompensa,
                      new GUIStyle(estiloMision) { alignment = TextAnchor.UpperLeft, fontSize = 15 });
        }
        else
        {
            GUIStyle fin = new GUIStyle(estiloMision) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            fin.normal.textColor = new Color(1f, 0.85f, 0.3f);
            GUI.Label(sr, "SENDA COMPLETA\n¡Eres un EINHERJAR!", fin);
        }

        // brujula simple: direccion al punto de descanso
        Vector3 aCasa = Hoguera.PuntoRespawn - Bjorn.transform.position;
        aCasa.y = 0f;
        if (aCasa.magnitude > 8f)
        {
            float angulo = Vector3.SignedAngle(Bjorn.transform.forward, aCasa, Vector3.up);
            string flecha = Mathf.Abs(angulo) < 30f ? "^" : angulo < 0f ? "<" : ">";
            LabelSombra(new Rect(Screen.width - 330, 126, 310, 28),
                        flecha + "  hoguera a " + Mathf.RoundToInt(aCasa.magnitude) + "m",
                        new GUIStyle(estiloMision) { fontSize = 16 });
        }

        if (Time.time < Bjorn.comboHasta && Bjorn.combo > 1)
        {
            GUIStyle comboSt = new GUIStyle(estiloHud) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            comboSt.normal.textColor = Bjorn.combo == 3 ? new Color(1f, 0.5f, 0.1f) : new Color(1f, 0.85f, 0.3f);
            LabelSombra(new Rect(cx - 150, cy + 34, 300, 36),
                        Bjorn.combo == 3 ? "¡ REMATE !" : "COMBO x" + Bjorn.combo, comboSt);
        }

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
                                                         : "Busca la RUNA DEL TRUENO explorando el mundo",
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
                    Bjorn.ReconstruirEspada();
                    sfxSrc.PlayOneShot(sfxImpacto);
                }
            }
        }

        float px = x0 + w - 300, py2 = y0 + 60;
        GUI.Label(new Rect(px, py2 - 26, 260, 24), "Asi se ve:", estiloHud);
        Color emp = Jugador.ColoresEmpunadura[PlayerPrefs.GetInt("forja_0", 0)];
        Color hoja = Jugador.ColoresHoja[PlayerPrefs.GetInt("forja_1", 0)];
        GUI.color = emp; GUI.DrawTexture(new Rect(px, py2 + 60, 26, 26), texBlanca);
        GUI.color = new Color(0.35f, 0.25f, 0.15f); GUI.DrawTexture(new Rect(px + 26, py2 + 66, 40, 14), texBlanca);
        GUI.color = emp; GUI.DrawTexture(new Rect(px + 66, py2 + 48, 14, 50), texBlanca);
        GUI.color = hoja; GUI.DrawTexture(new Rect(px + 80, py2 + 62, 150, 22), texBlanca);
        GUI.color = hoja; GUI.DrawTexture(new Rect(px + 230, py2 + 66, 22, 14), texBlanca);
        GUI.color = new Color(1f, 1f, 1f, 0.7f); GUI.DrawTexture(new Rect(px + 82, py2 + 64, 146, 4), texBlanca);
        GUI.color = Color.white;
    }

    void DibujarPoder(Rect r, string nombre, float carga, bool desbloqueado)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, texBlanca);
        GUI.color = desbloqueado ? new Color(1f, 0.85f, 0.2f, 0.85f) : new Color(0.4f, 0.4f, 0.4f, 0.6f);
        GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, (r.width - 4) * carga, r.height - 4), texBlanca);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 6, r.y + 6, r.width, r.height), nombre, estiloHud);
    }
}
