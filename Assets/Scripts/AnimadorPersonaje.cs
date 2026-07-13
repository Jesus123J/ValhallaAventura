using UnityEngine;
using System.Collections;

/// <summary>
/// Animacion procedimental de los personajes voxel en 3D:
/// - Caminar: brazos y piernas oscilan hacia adelante/atras (eje X).
/// - Atacar: el brazo del arma se alza sobre la cabeza y descarga el golpe.
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
        if (caminando) fase += Time.deltaTime * 10f;
        float ang = caminando ? Mathf.Sin(fase) * 35f : 0f;

        piernaDer.localRotation = Quaternion.Euler(ang, 0f, 0f);
        piernaIzq.localRotation = Quaternion.Euler(-ang, 0f, 0f);
        if (!atacando)
        {
            brazoDer.localRotation = Quaternion.Euler(-ang * 0.7f, 0f, 0f);
            brazoIzq.localRotation = Quaternion.Euler(ang * 0.7f, 0f, 0f);
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
        yield return Girar(brazoDer, 0f, -140f, 0.14f);  // alzar el arma
        yield return Girar(brazoDer, -140f, 40f, 0.09f); // ¡golpe!
        yield return Girar(brazoDer, 40f, 0f, 0.16f);    // volver
        atacando = false;
    }

    IEnumerator Girar(Transform t, float de, float a, float dur)
    {
        for (float x = 0f; x < dur; x += Time.deltaTime)
        {
            t.localRotation = Quaternion.Euler(Mathf.Lerp(de, a, x / dur), 0f, 0f);
            yield return null;
        }
        t.localRotation = Quaternion.Euler(a, 0f, 0f);
    }
}
