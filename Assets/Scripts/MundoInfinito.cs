using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MUNDO INFINITO estilo Minecraft: el terreno se genera en CHUNKS de 16x16
/// bloques alrededor del jugador mientras camina, y se descarga a lo lejos.
/// Cada chunk es UNA sola malla (rapido) con colores por vertice, su collider,
/// su decoracion por bioma (pinos, flores, rocas, brasas, picos de hielo)
/// y sus premios (almas, pociones, cofres, hogueras y la runa del trueno).
/// </summary>
public class MundoInfinito : MonoBehaviour
{
    public const int Tam = 16;      // bloques por lado de chunk
    public int radioVision = 5;     // chunks a la redonda
    [HideInInspector] public Transform jugador;

    readonly Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
    Material matVoxel;

    void Awake()
    {
        matVoxel = new Material(Shader.Find("Voxel/VertexColor"));
    }

    /// <summary>Genera de inmediato los chunks bajo los pies (para no caer al vacio al nacer).</summary>
    public void GenerarInmediato(Vector3 pos, int radio)
    {
        Vector2Int c = ChunkDe(pos);
        for (int dx = -radio; dx <= radio; dx++)
            for (int dz = -radio; dz <= radio; dz++)
                Asegurar(new Vector2Int(c.x + dx, c.y + dz));
    }

    void Update()
    {
        if (jugador == null) return;
        Vector2Int centro = ChunkDe(jugador.position);

        // crear los que faltan (presupuesto: 2 por frame, del centro hacia afuera)
        int creados = 0;
        for (int anillo = 0; anillo <= radioVision && creados < 2; anillo++)
        {
            for (int dx = -anillo; dx <= anillo && creados < 2; dx++)
            {
                for (int dz = -anillo; dz <= anillo && creados < 2; dz++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != anillo) continue;
                    Vector2Int c = new Vector2Int(centro.x + dx, centro.y + dz);
                    if (!chunks.ContainsKey(c)) { Asegurar(c); creados++; }
                }
            }
        }

        // descargar los lejanos
        List<Vector2Int> quitar = null;
        foreach (var kv in chunks)
        {
            if (Mathf.Max(Mathf.Abs(kv.Key.x - centro.x), Mathf.Abs(kv.Key.y - centro.y)) > radioVision + 2)
            {
                if (quitar == null) quitar = new List<Vector2Int>();
                quitar.Add(kv.Key);
            }
        }
        if (quitar != null)
            foreach (var c in quitar)
            {
                Destroy(chunks[c]);
                chunks.Remove(c);
            }
    }

    Vector2Int ChunkDe(Vector3 pos)
    {
        return new Vector2Int(Mathf.FloorToInt(pos.x / Tam), Mathf.FloorToInt(pos.z / Tam));
    }

    void Asegurar(Vector2Int c)
    {
        if (!chunks.ContainsKey(c))
            chunks[c] = CrearChunk(c.x, c.y);
    }

    // ============================================================
    //  CONSTRUCCION DE UN CHUNK
    // ============================================================
    GameObject CrearChunk(int cx, int cz)
    {
        var go = new GameObject("Chunk_" + cx + "_" + cz);
        go.transform.position = new Vector3(cx * Tam, 0f, cz * Tam);

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var cols = new List<Color>();

        for (int lx = 0; lx < Tam; lx++)
        {
            for (int lz = 0; lz < Tam; lz++)
            {
                int wx = cx * Tam + lx, wz = cz * Tam + lz;
                int h = GeneradorMundo.Altura(wx, wz);
                Color cTop = GeneradorMundo.ColorSuperficie(wx, wz, h);
                Color cLado = GeneradorMundo.ColorLadera(wx, wz);

                // cara superior (normal hacia ARRIBA)
                Quad(verts, tris, cols,
                     new Vector3(lx, h, lz), new Vector3(lx, h, lz + 1),
                     new Vector3(lx + 1, h, lz + 1), new Vector3(lx + 1, h, lz), cTop);

                // paredes hacia los vecinos mas bajos (cada una con su normal hacia AFUERA)
                int hE = GeneradorMundo.Altura(wx + 1, wz);
                if (hE < h) // pared este (+x)
                    Quad(verts, tris, cols,
                         new Vector3(lx + 1, hE, lz), new Vector3(lx + 1, h, lz),
                         new Vector3(lx + 1, h, lz + 1), new Vector3(lx + 1, hE, lz + 1), cLado);
                int hO = GeneradorMundo.Altura(wx - 1, wz);
                if (hO < h) // pared oeste (-x)
                    Quad(verts, tris, cols,
                         new Vector3(lx, hO, lz + 1), new Vector3(lx, h, lz + 1),
                         new Vector3(lx, h, lz), new Vector3(lx, hO, lz), cLado);
                int hN = GeneradorMundo.Altura(wx, wz + 1);
                if (hN < h) // pared norte (+z)
                    Quad(verts, tris, cols,
                         new Vector3(lx + 1, hN, lz + 1), new Vector3(lx + 1, h, lz + 1),
                         new Vector3(lx, h, lz + 1), new Vector3(lx, hN, lz + 1), cLado);
                int hS = GeneradorMundo.Altura(wx, wz - 1);
                if (hS < h) // pared sur (-z)
                    Quad(verts, tris, cols,
                         new Vector3(lx, hS, lz), new Vector3(lx, h, lz),
                         new Vector3(lx + 1, h, lz), new Vector3(lx + 1, hS, lz), cLado);
            }
        }

        var malla = new Mesh();
        malla.SetVertices(verts);
        malla.SetTriangles(tris, 0);
        malla.SetColors(cols);
        malla.RecalculateNormals();

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = malla;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = matVoxel;
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = malla;

        DecorarChunk(go.transform, cx, cz);
        return go;
    }

    void Quad(List<Vector3> v, List<int> t, List<Color> c,
              Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color color)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(d); v.Add(e);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
        t.Add(i); t.Add(i + 2); t.Add(i + 3);
        c.Add(color); c.Add(color); c.Add(color); c.Add(color);
    }

    // ============================================================
    //  DECORACION Y PREMIOS POR BIOMA (deterministico por chunk)
    // ============================================================
    void DecorarChunk(Transform chunk, int cx, int cz)
    {
        var rnd = new System.Random(cx * 73856093 ^ cz * 19349663 ^ GeneradorMundo.Semilla);
        int wxC = cx * Tam + Tam / 2, wzC = cz * Tam + Tam / 2;
        GeneradorMundo.TipoBioma bioma = GeneradorMundo.Bioma(wxC, wzC);

        Vector3 PosSuelo(out bool esHielo)
        {
            int lx = rnd.Next(1, Tam - 1), lz = rnd.Next(1, Tam - 1);
            int wx = cx * Tam + lx, wz = cz * Tam + lz;
            esHielo = GeneradorMundo.EsHielo(wx, wz);
            return new Vector3(wx + 0.5f, GeneradorMundo.Altura(wx, wz), wz + 0.5f);
        }

        // ---- vegetacion y detalles ----
        switch (bioma)
        {
            case GeneradorMundo.TipoBioma.Bosque:
                int pinos = 3 + rnd.Next(4);
                for (int i = 0; i < pinos; i++)
                {
                    Vector3 p = PosSuelo(out bool hielo);
                    if (hielo) continue;
                    CrearPino(chunk, p, false);
                }
                break;

            case GeneradorMundo.TipoBioma.Tundra:
                if (rnd.NextDouble() < 0.5)
                {
                    Vector3 p = PosSuelo(out bool hielo);
                    if (!hielo) CrearPino(chunk, p, true); // pino nevado
                }
                if (rnd.NextDouble() < 0.4)
                {
                    Vector3 p = PosSuelo(out _);
                    Transform pico = ConstructorPersonaje.Cubo(chunk, "PicoHielo", p + Vector3.up * 0.9f,
                        new Vector3(0.5f, 1.8f, 0.5f), new Color(0.7f, 0.88f, 1f, 0.9f));
                    pico.rotation = Quaternion.Euler(rnd.Next(-8, 8), rnd.Next(0, 90), rnd.Next(-8, 8));
                }
                break;

            case GeneradorMundo.TipoBioma.Pradera:
                int flores = 3 + rnd.Next(4);
                Color[] coloresFlor = { new Color(1f, 0.85f, 0.2f), new Color(0.9f, 0.3f, 0.3f), new Color(0.6f, 0.5f, 1f) };
                for (int i = 0; i < flores; i++)
                {
                    Vector3 p = PosSuelo(out bool hielo);
                    if (hielo) continue;
                    ConstructorPersonaje.Cubo(chunk, "Flor", p + Vector3.up * 0.2f,
                        new Vector3(0.14f, 0.4f, 0.14f), coloresFlor[rnd.Next(coloresFlor.Length)]);
                }
                break;

            case GeneradorMundo.TipoBioma.Ceniza:
                int brasas = 2 + rnd.Next(3);
                for (int i = 0; i < brasas; i++)
                {
                    Vector3 p = PosSuelo(out _);
                    ConstructorPersonaje.Cubo(chunk, "Brasa", p + Vector3.up * 0.15f,
                        new Vector3(0.3f, 0.3f, 0.3f), new Color(1f, 0.5f, 0.1f), true);
                }
                if (rnd.NextDouble() < 0.5)
                {
                    Vector3 p = PosSuelo(out _);
                    Transform roca = ConstructorPersonaje.Cubo(chunk, "RocaNegra", p + Vector3.up * 0.5f,
                        new Vector3(1.1f, 1f, 1f), new Color(0.16f, 0.13f, 0.13f));
                    roca.rotation = Quaternion.Euler(0f, rnd.Next(0, 90), 0f);
                }
                break;

            default: // montana
                if (rnd.NextDouble() < 0.6)
                {
                    Vector3 p = PosSuelo(out _);
                    Transform roca = ConstructorPersonaje.Cubo(chunk, "Roca", p + Vector3.up * 0.4f,
                        new Vector3(1.2f, 0.9f, 1f), new Color(0.4f, 0.42f, 0.48f));
                    roca.rotation = Quaternion.Euler(0f, rnd.Next(0, 90), 0f);
                }
                break;
        }

        // ---- premios ----
        if (rnd.NextDouble() < 0.30)
        {
            int n = 1 + rnd.Next(2);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = PosSuelo(out _);
                Recompensa.Crear(Recompensa.Tipo.Almas, p + Vector3.up * 1f).transform.SetParent(chunk, true);
            }
        }
        if (rnd.NextDouble() < 0.08)
        {
            Vector3 p = PosSuelo(out _);
            Recompensa.Crear(Recompensa.Tipo.Pocion, p + Vector3.up * 1f).transform.SetParent(chunk, true);
        }
        if (rnd.NextDouble() < 0.05)
        {
            Vector3 p = PosSuelo(out _);
            Cofre.Crear(p).transform.SetParent(chunk, true);
        }
        if (rnd.NextDouble() < 0.05)
        {
            Vector3 p = PosSuelo(out _);
            Hoguera.Crear(p).transform.SetParent(chunk, true);
        }
        // la Runa del Trueno aparece en el mundo hasta que la consigas
        if (PlayerPrefs.GetInt("poder_rayo", 0) == 0 && rnd.NextDouble() < 0.04)
        {
            Vector3 p = PosSuelo(out _);
            Recompensa.Crear(Recompensa.Tipo.RunaTrueno, p + Vector3.up * 1.2f).transform.SetParent(chunk, true);
        }
    }

    void CrearPino(Transform chunk, Vector3 p, bool nevado)
    {
        ConstructorPersonaje.Cubo(chunk, "Tronco", p + Vector3.up * 1.1f,
            new Vector3(0.35f, 2.2f, 0.35f), new Color(0.35f, 0.24f, 0.15f));
        Color hoja = nevado ? new Color(0.5f, 0.62f, 0.55f) : new Color(0.15f, 0.36f, 0.2f);
        for (int j = 0; j < 3; j++)
        {
            Transform copa = ConstructorPersonaje.Cubo(chunk, "Copa",
                p + Vector3.up * (2.2f + j * 0.7f),
                new Vector3(2.4f - j * 0.7f, 0.6f, 2.4f - j * 0.7f), hoja);
            copa.rotation = Quaternion.Euler(0f, 45f, 0f);
        }
        if (nevado)
            ConstructorPersonaje.Cubo(chunk, "NieveCopa", p + Vector3.up * 3.8f,
                new Vector3(0.7f, 0.25f, 0.7f), new Color(0.9f, 0.94f, 1f));
    }
}
