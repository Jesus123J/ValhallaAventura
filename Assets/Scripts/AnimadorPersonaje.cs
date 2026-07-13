using UnityEngine;
using System.Collections;

/// <summary>
/// Animacion 2D procedimental del titere:
/// - Caminar: piernas y brazos oscilan (onda seno) girando en Z.
/// - Atacar: el brazo de la espada echa atras y descarga un tajo hacia adelante.
/// </summary>
public class AnimadorPersonaje : MonoBehaviour
{
    [HideInInspector] public Transform brazoDer, brazoIzq, piernaDer, piernaIzq;

    private bool caminando;
    private bool atacando;
    private float fase;

    public void Caminando(bool si) { caminando = si; }

    void Update()
    {
        if (caminando) fase += Time.deltaTime * 11f;
        float ang = caminando ? Mathf.Sin(fase) * 32f : 0f;

        piernaDer.localRotation = Quaternion.Euler(0f, 0f, ang);
        piernaIzq.localRotation = Quaternion.Euler(0f, 0f, -ang);
        if (!atacando)
        {
            brazoDer.localRotation = Quaternion.Euler(0f, 0f, -ang * 0.7f);
            brazoIzq.localRotation = Quaternion.Euler(0f, 0f, ang * 0.7f);
        }
    }

    public void Atacar()
    {
        if (!atacando && gameObject.activeInHierarchy)
            StartCoroutine(AnimAtaque());
    }

    IEnumerator AnimAtaque()
    {
        atacando = true;
        yield return Girar(brazoDer, 0f, -110f, 0.09f);  // echar la espada atras
        yield return Girar(brazoDer, -110f, 95f, 0.08f); // ¡tajo hacia adelante!
        yield return Girar(brazoDer, 95f, 0f, 0.15f);    // volver
        atacando = false;
    }

    IEnumerator Girar(Transform t, float de, float a, float dur)
    {
        for (float x = 0f; x < dur; x += Time.deltaTime)
        {
            t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(de, a, x / dur));
            yield return null;
        }
        t.localRotation = Quaternion.Euler(0f, 0f, a);
    }
}
