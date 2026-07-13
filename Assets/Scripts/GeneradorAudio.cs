using UnityEngine;

/// <summary>
/// Genera la musica y los efectos de sonido POR CODIGO (sintesis matematica),
/// sin archivos de audio.
/// </summary>
public static class GeneradorAudio
{
    const int FS = 22050;

    // ==================== EFECTOS DE COMBATE ====================

    /// <summary>Silbido del hacha al cortar el aire (ruido blanco filtrado).</summary>
    public static AudioClip Espada()
    {
        System.Random r = new System.Random(1234);
        float dur = 0.16f;
        int n = Mathf.RoundToInt(dur * FS);
        float[] datos = new float[n];
        float filtrado = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / FS;
            float ruido = (float)(r.NextDouble() * 2.0 - 1.0);
            filtrado = filtrado * 0.72f + ruido * 0.28f; // filtro paso-bajo simple
            datos[i] = filtrado * Envolvente(t, dur) * 0.5f;
        }
        return Clip("espada", datos);
    }

    /// <summary>Golpe seco al impactar a un enemigo (tono grave + chasquido).</summary>
    public static AudioClip Impacto()
    {
        System.Random r = new System.Random(777);
        float dur = 0.16f;
        int n = Mathf.RoundToInt(dur * FS);
        float[] datos = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / FS;
            float grave = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 22f) * 0.7f;
            float chasquido = (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 70f) * 0.35f;
            datos[i] = grave + chasquido;
        }
        return Clip("impacto", datos);
    }

    /// <summary>Tonos descendentes: el jugador recibe dano.</summary>
    public static AudioClip Herido()
    {
        return Secuencia("herido", new[] { 330f, 262f }, 0.13f, 0.4f);
    }

    /// <summary>Fanfarria de victoria.</summary>
    public static AudioClip Fanfarria()
    {
        return Secuencia("fanfarria", new[] { 440f, 523f, 659f, 880f, 659f, 880f }, 0.16f, 0.45f);
    }

    /// <summary>Arpegio brillante al ganar almas.</summary>
    public static AudioClip Almas()
    {
        return Secuencia("almas", new[] { 660f, 880f, 1100f }, 0.07f, 0.35f);
    }

    /// <summary>Rayo de Odin: zumbido electrico que cae en picada.</summary>
    public static AudioClip Trueno()
    {
        System.Random r = new System.Random(4242);
        float dur = 0.35f;
        int n = Mathf.RoundToInt(dur * FS);
        float[] datos = new float[n];
        float faseAcum = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / FS;
            float freq = Mathf.Lerp(1300f, 180f, t / dur);   // barrido descendente
            faseAcum += freq / FS;
            float zumbido = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * faseAcum)) * 0.30f; // onda cuadrada
            float crujido = (float)(r.NextDouble() * 2.0 - 1.0) * 0.18f;
            datos[i] = (zumbido + crujido) * Envolvente(t, dur);
        }
        return Clip("trueno", datos);
    }

    // ==================== MUSICA ====================

    /// <summary>
    /// Musica de batalla nordica en bucle: drone grave (cuerno de guerra),
    /// melodia pentatonica menor grave y tambor de guerra.
    /// </summary>
    public static AudioClip MusicaNordica()
    {
        float[] escala = { 164.81f, 196f, 220f, 246.94f, 293.66f, 329.63f }; // Mi menor pent. grave
        int[] melodia =
        {
            0, 2, 3, 2,   0, -1, 0, 2,
            4, 3, 2, 3,   2, 0, -1, -1,
            0, 2, 3, 4,   5, 4, 3, 2,
            3, 2, 0, 2,   0, -1, -1, -1
        };

        float beat = 60f / 80f; // 80 bpm, marcial
        int total = Mathf.RoundToInt(FS * beat * melodia.Length);
        float[] datos = new float[total];

        // Drone: quinta abierta grave (Mi2 + Si2)
        for (int i = 0; i < total; i++)
        {
            float t = (float)i / FS;
            datos[i] += Mathf.Sin(2f * Mathf.PI * 82.41f * t) * 0.11f;
            datos[i] += Mathf.Sin(2f * Mathf.PI * 123.47f * t) * 0.06f;
        }

        // Melodia
        for (int k = 0; k < melodia.Length; k++)
        {
            if (melodia[k] < 0) continue;
            float freq = escala[melodia[k]];
            int inicio = Mathf.RoundToInt(k * beat * FS);
            float durNota = beat * 0.9f;
            int n = Mathf.RoundToInt(durNota * FS);
            for (int i = 0; i < n && inicio + i < total; i++)
            {
                float t = (float)i / FS;
                datos[inicio + i] += Triangulo(t * freq) * Envolvente(t, durNota) * 0.18f;
            }
        }

        // Tambor de guerra: negra 1 y 3 de cada compas
        for (int k = 0; k < melodia.Length; k += 2)
        {
            int inicio = Mathf.RoundToInt(k * beat * FS);
            int n = Mathf.RoundToInt(0.18f * FS);
            float fuerza = (k % 4 == 0) ? 0.55f : 0.3f;
            for (int i = 0; i < n && inicio + i < total; i++)
            {
                float t = (float)i / FS;
                datos[inicio + i] += Mathf.Sin(2f * Mathf.PI * 58f * t) * Mathf.Exp(-t * 20f) * fuerza;
            }
        }

        return Clip("musica_batalla", datos);
    }

    // ==================== UTILIDADES ====================

    static AudioClip Clip(string nombre, float[] datos)
    {
        AudioClip clip = AudioClip.Create(nombre, datos.Length, 1, FS, false);
        clip.SetData(datos, 0);
        return clip;
    }

    static AudioClip Secuencia(string nombre, float[] notas, float durNota, float vol)
    {
        int porNota = Mathf.RoundToInt(durNota * FS);
        float[] datos = new float[porNota * notas.Length];
        for (int k = 0; k < notas.Length; k++)
        {
            for (int i = 0; i < porNota; i++)
            {
                float t = (float)i / FS;
                datos[k * porNota + i] = Triangulo(t * notas[k]) * Envolvente(t, durNota) * vol;
            }
        }
        return Clip(nombre, datos);
    }

    static float Triangulo(float fase)
    {
        float f = fase - Mathf.Floor(fase);
        return 4f * Mathf.Abs(f - 0.5f) - 1f;
    }

    static float Envolvente(float t, float dur)
    {
        float ataque = Mathf.Min(1f, t * 120f);
        float caida = Mathf.Exp(-3.5f * t / dur);
        return ataque * caida;
    }
}
