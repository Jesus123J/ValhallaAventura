using UnityEngine;

/// <summary>
/// El corazon del MUNDO ABIERTO: funciones matematicas (ruido Perlin) que
/// definen un mundo infinito y deterministico a partir de una SEMILLA.
/// Mismo x,z -> siempre la misma altura y el mismo bioma: tu mundo es tuyo.
///
/// BIOMAS (se cruzan caminando, sin puertas):
/// - TUNDRA HELADA: nieve blanca y lagos de hielo
/// - BOSQUE DE PINOS: pasto oscuro y pinos nevados
/// - PRADERA VERDE: hierba clara y flores
/// - TIERRAS DE CENIZA: suelo quemado con vetas de lava
/// - MONTANAS GRISES: picos altos de piedra con cumbres nevadas
/// </summary>
public static class GeneradorMundo
{
    public enum TipoBioma { Tundra, Bosque, Pradera, Ceniza, Montana }

    public static int Semilla { get; private set; }
    static float ox, oz;

    public static void Iniciar(int semilla)
    {
        Semilla = semilla;
        var r = new System.Random(semilla);
        ox = (float)r.NextDouble() * 10000f;
        oz = (float)r.NextDouble() * 10000f;
    }

    static float Ruido(float x, float z, float escala, float sal)
    {
        return Mathf.PerlinNoise((x + ox + sal) * escala, (z + oz + sal) * escala);
    }

    static float Montanosidad(int x, int z) { return Ruido(x, z, 0.008f, 300f); }

    static float AlturaCruda(int x, int z)
    {
        float baseR = Ruido(x, z, 0.035f, 0f);
        float m = Montanosidad(x, z);
        float extra = Mathf.SmoothStep(0f, 1f, (m - 0.6f) / 0.25f);
        return 2.5f + baseR * 4.5f + extra * (10f + Ruido(x, z, 0.05f, 50f) * 9f);
    }

    /// <summary>Altura del bloque superior (los lagos se congelan al nivel 3).</summary>
    public static int Altura(int x, int z)
    {
        return Mathf.Max(3, Mathf.FloorToInt(AlturaCruda(x, z)));
    }

    public static bool EsHielo(int x, int z)
    {
        return AlturaCruda(x, z) < 3.1f && Bioma(x, z) != TipoBioma.Ceniza;
    }

    public static TipoBioma Bioma(int x, int z)
    {
        if (Montanosidad(x, z) > 0.68f) return TipoBioma.Montana;
        float t = Ruido(x, z, 0.0045f, 700f);
        if (t < 0.32f) return TipoBioma.Tundra;
        if (t < 0.55f) return TipoBioma.Bosque;
        if (t < 0.76f) return TipoBioma.Pradera;
        return TipoBioma.Ceniza;
    }

    public static string NombreBioma(TipoBioma b)
    {
        switch (b)
        {
            case TipoBioma.Tundra: return "TUNDRA HELADA";
            case TipoBioma.Bosque: return "BOSQUE DE PINOS";
            case TipoBioma.Pradera: return "PRADERA VERDE";
            case TipoBioma.Ceniza: return "TIERRAS DE CENIZA";
            default: return "MONTANAS GRISES";
        }
    }

    /// <summary>Color del bloque de la superficie (con variacion sutil, como texturas).</summary>
    public static Color ColorSuperficie(int x, int z, int h)
    {
        float variacion = 0.92f + Ruido(x, z, 0.35f, 40f) * 0.14f;
        if (EsHielo(x, z)) return new Color(0.62f, 0.80f, 0.95f) * variacion;

        switch (Bioma(x, z))
        {
            case TipoBioma.Tundra: return new Color(0.88f, 0.92f, 0.97f) * variacion;
            case TipoBioma.Bosque: return new Color(0.24f, 0.48f, 0.26f) * variacion;
            case TipoBioma.Pradera: return new Color(0.44f, 0.66f, 0.30f) * variacion;
            case TipoBioma.Ceniza:
                if (Ruido(x, z, 0.2f, 900f) > 0.82f) return new Color(1f, 0.45f, 0.08f); // veta de LAVA
                return new Color(0.27f, 0.23f, 0.23f) * variacion;
            default: // montana: cumbres nevadas
                return (h >= 13 ? new Color(0.90f, 0.93f, 0.97f) : new Color(0.48f, 0.50f, 0.55f)) * variacion;
        }
    }

    /// <summary>Color de las paredes laterales de los bloques (la "tierra").</summary>
    public static Color ColorLadera(int x, int z)
    {
        switch (Bioma(x, z))
        {
            case TipoBioma.Montana: return new Color(0.42f, 0.44f, 0.50f);
            case TipoBioma.Ceniza: return new Color(0.20f, 0.17f, 0.17f);
            case TipoBioma.Tundra: return new Color(0.55f, 0.50f, 0.45f);
            default: return new Color(0.42f, 0.31f, 0.20f);
        }
    }
}
