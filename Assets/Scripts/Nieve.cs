using UnityEngine;

/// <summary>
/// Nieve 3D: copos que caen alrededor del jugador en todas direcciones
/// y reaparecen arriba, siguiendote por el campo de batalla.
/// </summary>
public class Nieve : MonoBehaviour
{
    [HideInInspector] public Transform centro;
    public int cantidad = 80;

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
            float tam = Random.Range(0.05f, 0.11f);
            Transform copo = ConstructorPersonaje.Rect(transform, "Copo_" + i,
                Vector2.zero, new Vector2(tam, tam),
                new Color(1f, 1f, 1f, Random.Range(0.35f, 0.8f)), 0, tam);
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
            if (p.y < 0f)
                p = Reposicionar(false);
            copos[i].position = p;
        }
    }

    Vector3 Reposicionar(bool alturaAleatoria)
    {
        Vector3 c = centro != null ? centro.position : Vector3.zero;
        return new Vector3(c.x + Random.Range(-16f, 16f),
                           alturaAleatoria ? Random.Range(1f, 11f) : Random.Range(9f, 12f),
                           c.z + Random.Range(-16f, 16f));
    }
}
