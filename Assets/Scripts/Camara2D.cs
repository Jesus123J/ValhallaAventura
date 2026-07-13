using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Camara 2D de nivel lateral: sigue a Bjorn suavemente por el nivel
/// y mueve las capas del fondo a distinta velocidad (parallax)
/// para dar sensacion de profundidad.
/// </summary>
public class Camara2D : MonoBehaviour
{
    [HideInInspector] public Transform objetivo;
    public float minX = -6f;
    public float maxX = 120f;

    private readonly List<Capa> capas = new List<Capa>();
    private Vector3 posInicial;
    private bool inicializada;

    class Capa
    {
        public Transform t;
        public float factor;      // 0 = pegado a la camara (muy lejos), 1 = fijo al mundo
        public Vector3 posBase;
    }

    /// <summary>Registra una capa de fondo con su factor de parallax.</summary>
    public void RegistrarParallax(Transform capa, float factor)
    {
        capas.Add(new Capa { t = capa, factor = factor, posBase = capa.position });
    }

    void LateUpdate()
    {
        if (objetivo == null) return;
        if (!inicializada) { posInicial = transform.position; inicializada = true; }

        float x = Mathf.Clamp(objetivo.position.x + 1.5f, minX, maxX);
        float y = Mathf.Lerp(transform.position.y, Mathf.Max(3.2f, objetivo.position.y + 1.8f), 4f * Time.deltaTime);
        transform.position = new Vector3(x, y, -10f);

        // parallax: cuanto menor el factor, mas "lejos" se siente la capa
        Vector3 delta = transform.position - posInicial;
        foreach (Capa c in capas)
            c.t.position = c.posBase + new Vector3(delta.x * (1f - c.factor), delta.y * 0.15f * (1f - c.factor), 0f);
    }
}
