using UnityEngine;

/// <summary>
/// Premios 3D: giran sobre si mismos, flotan y brillan en el campo.
/// - ALMAS: runa dorada (+25)
/// - POCION: hidromiel curativa (+30 vida)
/// - RUNA DEL TRUENO: desbloquea el Rayo de Odin (Q)
/// </summary>
public class Recompensa : MonoBehaviour
{
    public enum Tipo { Almas, Pocion, RunaTrueno }
    [HideInInspector] public Tipo tipo;

    private Vector3 posBase;
    private float fase;

    public static Recompensa Crear(Tipo tipo, Vector3 pos)
    {
        GameObject go = new GameObject("Premio_" + tipo);
        go.transform.position = pos;
        Recompensa r = go.AddComponent<Recompensa>();
        r.tipo = tipo;
        Transform t = go.transform;

        switch (tipo)
        {
            case Tipo.Almas:
                ConstructorPersonaje.Circ(t, "Halo", Vector2.zero, 0.9f, new Color(1f, 0.85f, 0.3f, 0.25f), 0, true);
                Transform rombo = ConstructorPersonaje.Rect(t, "Runa", Vector2.zero, new Vector2(0.34f, 0.34f), new Color(1f, 0.8f, 0.15f), 0, 0.1f, true);
                rombo.localRotation = Quaternion.Euler(0f, 0f, 45f);
                ConstructorPersonaje.Rect(t, "Marca", Vector2.zero, new Vector2(0.07f, 0.22f), new Color(0.55f, 0.35f, 0.05f), 1, 0.12f);
                break;

            case Tipo.Pocion:
                ConstructorPersonaje.Circ(t, "Halo", Vector2.zero, 0.95f, new Color(1f, 0.3f, 0.35f, 0.22f), 0, true);
                ConstructorPersonaje.Rect(t, "Frasco", Vector2.zero, new Vector2(0.34f, 0.44f), new Color(0.85f, 0.15f, 0.2f), 0, 0.3f);
                ConstructorPersonaje.Rect(t, "Tapa", new Vector2(0f, 0.27f), new Vector2(0.16f, 0.12f), new Color(0.5f, 0.35f, 0.2f), 0, 0.16f);
                ConstructorPersonaje.Rect(t, "Cruz1", Vector2.zero, new Vector2(0.2f, 0.06f), Color.white, 1, 0.06f);
                ConstructorPersonaje.Rect(t, "Cruz2", Vector2.zero, new Vector2(0.06f, 0.2f), Color.white, 1, 0.06f);
                break;

            case Tipo.RunaTrueno:
                ConstructorPersonaje.Circ(t, "Halo", Vector2.zero, 1.8f, new Color(1f, 0.95f, 0.3f, 0.3f), 0, true);
                ConstructorPersonaje.Rect(t, "Piedra", Vector2.zero, new Vector2(0.55f, 0.7f), new Color(0.35f, 0.38f, 0.45f), 0, 0.3f);
                Transform z1 = ConstructorPersonaje.Rect(t, "Z1", new Vector2(0.04f, 0.15f), new Vector2(0.3f, 0.08f), new Color(1f, 0.95f, 0.3f), 1, 0.1f, true);
                z1.localRotation = Quaternion.Euler(0f, 0f, -55f);
                Transform z2 = ConstructorPersonaje.Rect(t, "Z2", new Vector2(-0.02f, 0f), new Vector2(0.3f, 0.08f), new Color(1f, 0.95f, 0.3f), 1, 0.1f, true);
                z2.localRotation = Quaternion.Euler(0f, 0f, 55f);
                Transform z3 = ConstructorPersonaje.Rect(t, "Z3", new Vector2(0.04f, -0.16f), new Vector2(0.3f, 0.08f), new Color(1f, 0.95f, 0.3f), 1, 0.1f, true);
                z3.localRotation = Quaternion.Euler(0f, 0f, -55f);

                Light luz = t.gameObject.AddComponent<Light>();
                luz.type = LightType.Point;
                luz.color = new Color(1f, 0.95f, 0.3f);
                luz.range = 8f;
                luz.intensity = 2.5f;
                break;
        }
        return r;
    }

    void Start()
    {
        posBase = transform.position;
        fase = transform.position.x * 1.7f;
    }

    void Update()
    {
        // girar como item de juego + flotar
        transform.Rotate(0f, 80f * Time.deltaTime, 0f, Space.World);
        transform.position = posBase + Vector3.up * Mathf.Sin(Time.time * 2.5f + fase) * 0.15f;

        Jugador j = GestorAventura.Instancia != null ? GestorAventura.Instancia.Bjorn : null;
        if (j == null) return;
        Vector3 d = j.transform.position + Vector3.up * 1f - transform.position;
        if (d.magnitude < 1.3f)
        {
            GestorAventura.Instancia.Recoger(this);
            Destroy(gameObject);
        }
    }
}
