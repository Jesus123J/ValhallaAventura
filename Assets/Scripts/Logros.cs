using UnityEngine;

/// <summary>
/// Sistema de LOGROS: 20 hazañas que se desbloquean jugando y quedan
/// guardadas para siempre (PlayerPrefs). Al desbloquear uno aparece un
/// aviso con fanfarria. La coleccion se ve en el panel de TAB.
/// Tambien lleva CONTADORES persistentes (draugr, saltos, almas...).
/// </summary>
public static class Logros
{
    public class Def
    {
        public string id, nombre, desc;
        public Def(string i, string n, string d) { id = i; nombre = n; desc = d; }
    }

    public static readonly Def[] Lista =
    {
        new Def("primera_sangre", "Primera Sangre",     "Derrota a tu primer draugr"),
        new Def("cazador",        "Cazador de Draugr",  "Derrota 10 draugr en total"),
        new Def("exterminador",   "Exterminador",       "Derrota 50 draugr en total"),
        new Def("jefe",           "Guardian Caido",     "Derrota al jefe guardian"),
        new Def("parry1",         "¡Parry!",            "Deten un golpe en el momento justo"),
        new Def("parry10",        "Maestro del Acero",  "Haz 10 parries"),
        new Def("cargado",        "Torbellino",         "Usa el golpe cargado giratorio"),
        new Def("rayo",           "Tormenta Viva",      "Dispara el Rayo de Odin"),
        new Def("trueno",         "Furia del Cielo",    "Compra el Trueno de Odin"),
        new Def("intocable",      "Intocable",          "Capitulo completo sin recibir dano"),
        new Def("veloz",          "Pies Ligeros",       "Capitulo en menos de 5 minutos"),
        new Def("codicioso",      "Codicioso",          "Las 14 runas doradas en una partida"),
        new Def("rico",           "Bolsa Llena",        "Gana 1000 almas en total"),
        new Def("millonario",     "Tesoro de Rey",      "Gana 3000 almas en total"),
        new Def("comprador",      "Aprendiz",           "Compra tu primera habilidad"),
        new Def("arbol",          "Arbol Completo",     "Todas las habilidades al maximo"),
        new Def("saltarin",       "Saltarin",           "Salta 100 veces"),
        new Def("bailarin",       "Bailarin de Guerra", "Haz 50 dashes"),
        new Def("abridor",        "Buscador de Tesoros","Abre 3 cofres"),
        new Def("valhalla",       "Digno del Valhalla", "Termina con rango VALHALLA")
    };

    public static bool Tiene(string id)
    {
        return PlayerPrefs.GetInt("logro_" + id, 0) == 1;
    }

    public static int TotalDesbloqueados()
    {
        int n = 0;
        foreach (Def d in Lista) if (Tiene(d.id)) n++;
        return n;
    }

    public static void Desbloquear(string id)
    {
        if (Tiene(id)) return;
        PlayerPrefs.SetInt("logro_" + id, 1);
        PlayerPrefs.Save();
        foreach (Def d in Lista)
            if (d.id == id && GestorAventura.Instancia != null)
                GestorAventura.Instancia.LogroDesbloqueado(d.nombre);
    }

    /// <summary>Suma a un contador persistente y devuelve el total acumulado.</summary>
    public static int Contar(string clave, int incremento)
    {
        int v = PlayerPrefs.GetInt("cont_" + clave, 0) + incremento;
        PlayerPrefs.SetInt("cont_" + clave, v);
        return v;
    }
}
