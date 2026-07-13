// Shader simple con color por vertice + iluminacion: cada bloque del terreno
// lleva su color pintado en el vertice (nieve, pasto, piedra, hielo, lava...)
Shader "Voxel/VertexColor"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 150

        CGPROGRAM
        #pragma surface surf Lambert

        struct Input
        {
            float4 color : COLOR;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
