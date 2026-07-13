using UnityEngine;

/// <summary>
/// Numero de dano flotante: aparece donde golpeas, sube, mira siempre
/// a la camara y se desvanece. Los CRITICOS salen mas grandes y naranjas.
/// </summary>
public class DanoFlotante : MonoBehaviour
{
    private TextMesh texto;
    private float vida = 0.75f;
    private Vector3 deriva;

    public static void Crear(Vector3 pos, float cantidad, bool critico)
    {
        GameObject go = new GameObject("Dano");
        go.transform.position = pos + new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f));
        DanoFlotante d = go.AddComponent<DanoFlotante>();

        d.texto = go.AddComponent<TextMesh>();
        d.texto.text = critico ? "¡" + Mathf.RoundToInt(cantidad) + "!" : Mathf.RoundToInt(cantidad).ToString();
        d.texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        d.texto.GetComponent<MeshRenderer>().material = d.texto.font.material;
        d.texto.fontSize = 48;
        d.texto.characterSize = critico ? 0.14f : 0.09f;
        d.texto.anchor = TextAnchor.MiddleCenter;
        d.texto.fontStyle = FontStyle.Bold;
        d.texto.color = critico ? new Color(1f, 0.55f, 0.1f) : new Color(1f, 0.9f, 0.3f);
        d.deriva = new Vector3(Random.Range(-0.4f, 0.4f), 2.2f, 0f);
    }

    void Update()
    {
        vida -= Time.deltaTime;
        if (vida <= 0f) { Destroy(gameObject); return; }

        transform.position += deriva * Time.deltaTime;
        deriva.y = Mathf.Max(0.6f, deriva.y - 4f * Time.deltaTime);

        // siempre de cara a la camara
        if (Camera.main != null)
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);

        Color c = texto.color;
        c.a = Mathf.Clamp01(vida / 0.35f);
        texto.color = c;
    }
}
