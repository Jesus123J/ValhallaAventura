using UnityEngine;

/// <summary>
/// Nieve 2D: copos blancos que caen alrededor de la camara,
/// con balanceo, y reaparecen arriba siguiendo el avance por el nivel.
/// </summary>
public class Nieve2D : MonoBehaviour
{
    [HideInInspector] public Transform camara;
    public int cantidad = 60;

    private Transform[] copos;
    private float[] velocidades;
    private float[] fases;

    void Start()
    {
        copos = new Transform[cantidad];
        velocidades = new float[cantidad];
        fases = new float[cantidad];

        for (int i = 0; i < cantidad; i++)
        {
            float tam = Random.Range(0.05f, 0.13f);
            Transform copo = ConstructorPersonaje.Rect(transform, "Copo_" + i,
                Vector2.zero, new Vector2(tam, tam),
                new Color(1f, 1f, 1f, Random.Range(0.35f, 0.8f)), 12);
            copo.position = Reposicionar(true);
            copos[i] = copo;
            velocidades[i] = Random.Range(0.8f, 2.2f);
            fases[i] = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    void Update()
    {
        for (int i = 0; i < copos.Length; i++)
        {
            Vector3 p = copos[i].position;
            p.y -= velocidades[i] * Time.deltaTime;
            p.x += Mathf.Sin(Time.time * 1.4f + fases[i]) * 0.5f * Time.deltaTime;
            if (p.y < camara.position.y - 6.5f)
                p = Reposicionar(false);
            copos[i].position = p;
        }
    }

    Vector3 Reposicionar(bool alturaAleatoria)
    {
        Vector3 c = camara != null ? camara.position : Vector3.zero;
        return new Vector3(c.x + Random.Range(-12f, 12f),
                           alturaAleatoria ? c.y + Random.Range(-5f, 6f) : c.y + Random.Range(6f, 8f),
                           0f);
    }
}
