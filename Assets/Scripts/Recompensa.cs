using UnityEngine;

/// <summary>
/// Premios recogibles del nivel (flotan y brillan; se recogen al tocarlos):
/// - ALMAS: runa dorada, +25 almas.
/// - POCION: hidromiel curativa, +30 de vida.
/// - RUNA DEL TRUENO: desbloquea el poder Rayo de Odin (tecla Q).
/// </summary>
public class Recompensa : MonoBehaviour
{
    public enum Tipo { Almas, Pocion, RunaTrueno }
    [HideInInspector] public Tipo tipo;

    private Vector3 posBase;
    private float fase;

    public static Recompensa Crear(Tipo tipo, Vector2 pos)
    {
        GameObject go = new GameObject("Premio_" + tipo);
        go.transform.position = pos;
        Recompensa r = go.AddComponent<Recompensa>();
        r.tipo = tipo;
        Transform t = go.transform;

        switch (tipo)
        {
            case Tipo.Almas: // runa dorada en rombo con halo
                ConstructorPersonaje.Circ(t, "Halo", Vector2.zero, 0.85f, new Color(1f, 0.85f, 0.3f, 0.25f), 7);
                Transform rombo = ConstructorPersonaje.Rect(t, "Runa", Vector2.zero, new Vector2(0.34f, 0.34f), new Color(1f, 0.8f, 0.15f), 8);
                rombo.localRotation = Quaternion.Euler(0f, 0f, 45f);
                ConstructorPersonaje.Rect(t, "Marca", Vector2.zero, new Vector2(0.07f, 0.22f), new Color(0.55f, 0.35f, 0.05f), 9);
                break;

            case Tipo.Pocion: // cuerno de hidromiel rojo curativo
                ConstructorPersonaje.Circ(t, "Halo", Vector2.zero, 0.9f, new Color(1f, 0.3f, 0.35f, 0.22f), 7);
                ConstructorPersonaje.Rect(t, "Frasco", Vector2.zero, new Vector2(0.34f, 0.44f), new Color(0.85f, 0.15f, 0.2f), 8);
                ConstructorPersonaje.Rect(t, "Tapa", new Vector2(0f, 0.27f), new Vector2(0.16f, 0.12f), new Color(0.5f, 0.35f, 0.2f), 9);
                ConstructorPersonaje.Rect(t, "Cruz1", Vector2.zero, new Vector2(0.2f, 0.06f), Color.white, 9);
                ConstructorPersonaje.Rect(t, "Cruz2", Vector2.zero, new Vector2(0.06f, 0.2f), Color.white, 9);
                break;

            case Tipo.RunaTrueno: // runa electrica amarilla, grande y pulsante
                ConstructorPersonaje.Circ(t, "Halo", Vector2.zero, 1.6f, new Color(1f, 0.95f, 0.3f, 0.3f), 7);
                ConstructorPersonaje.Rect(t, "Piedra", Vector2.zero, new Vector2(0.55f, 0.7f), new Color(0.35f, 0.38f, 0.45f), 8);
                // rayo en zigzag (3 trazos)
                Transform z1 = ConstructorPersonaje.Rect(t, "Z1", new Vector2(0.04f, 0.15f), new Vector2(0.3f, 0.08f), new Color(1f, 0.95f, 0.3f), 9);
                z1.localRotation = Quaternion.Euler(0f, 0f, -55f);
                Transform z2 = ConstructorPersonaje.Rect(t, "Z2", new Vector2(-0.02f, 0f), new Vector2(0.3f, 0.08f), new Color(1f, 0.95f, 0.3f), 9);
                z2.localRotation = Quaternion.Euler(0f, 0f, 55f);
                Transform z3 = ConstructorPersonaje.Rect(t, "Z3", new Vector2(0.04f, -0.16f), new Vector2(0.3f, 0.08f), new Color(1f, 0.95f, 0.3f), 9);
                z3.localRotation = Quaternion.Euler(0f, 0f, -55f);
                break;
        }
        return r;
    }

    void Start()
    {
        posBase = transform.position;
        fase = transform.position.x * 1.7f; // desincroniza el flote entre premios
    }

    void Update()
    {
        // flotar y latir
        transform.position = posBase + Vector3.up * Mathf.Sin(Time.time * 2.5f + fase) * 0.15f;
        float pulso = 1f + Mathf.Sin(Time.time * 4f + fase) * (tipo == Tipo.RunaTrueno ? 0.12f : 0.06f);
        transform.localScale = Vector3.one * pulso;

        // recoger al tocar
        Jugador j = GestorAventura.Instancia != null ? GestorAventura.Instancia.Bjorn : null;
        if (j == null) return;
        float dx = Mathf.Abs(j.transform.position.x - transform.position.x);
        float dy = Mathf.Abs(j.transform.position.y + 1f - transform.position.y);
        if (dx < 0.75f && dy < 1.2f)
        {
            GestorAventura.Instancia.Recoger(this);
            Destroy(gameObject);
        }
    }
}
