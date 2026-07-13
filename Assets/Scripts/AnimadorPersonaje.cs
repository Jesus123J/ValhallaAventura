using UnityEngine;
using System.Collections;

/// <summary>
/// Animacion procedimental del personaje voxel, con vida:
/// - Caminar: brazos y piernas oscilan + el CUERPO rebota y se inclina adelante.
/// - Reposo: respiracion sutil (el cuerpo sube y baja despacio).
/// - Atacar: alza el arma sobre la cabeza y la descarga.
/// </summary>
public class AnimadorPersonaje : MonoBehaviour
{
    [HideInInspector] public Transform brazoDer, brazoIzq, piernaDer, piernaIzq;

    private bool caminando;
    private bool atacando;
    private float fase;
    private float inclinacion;
    private Vector3 posBase;

    void Start()
    {
        posBase = transform.localPosition;
    }

    public void Caminando(bool si) { caminando = si; }

    void Update()
    {
        float ang;
        float rebote;

        if (caminando)
        {
            fase += Time.deltaTime * 10f;
            ang = Mathf.Sin(fase) * 35f;
            rebote = Mathf.Abs(Mathf.Sin(fase)) * 0.07f;                 // rebota al caminar
            inclinacion = Mathf.Lerp(inclinacion, 6f, 6f * Time.deltaTime); // se inclina adelante
        }
        else
        {
            ang = 0f;
            rebote = Mathf.Sin(Time.time * 2f) * 0.02f;                  // respira en reposo
            inclinacion = Mathf.Lerp(inclinacion, 0f, 6f * Time.deltaTime);
        }

        transform.localPosition = posBase + Vector3.up * rebote;
        transform.localRotation = Quaternion.Euler(inclinacion, 0f, 0f);

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
        yield return Girar(brazoDer, 0f, -140f, 0.14f);
        yield return Girar(brazoDer, -140f, 40f, 0.09f);
        yield return Girar(brazoDer, 40f, 0f, 0.16f);
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
