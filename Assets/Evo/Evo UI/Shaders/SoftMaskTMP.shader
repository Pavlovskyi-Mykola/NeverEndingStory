Shader "Hidden/Evo/UI/Soft Mask TMP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Font Atlas", 2D) = "white" {}
        _FaceColor ("Face Color", Color) = (1,1,1,1)
        _FaceDilate ("Face Dilate", Range(-1,1)) = 0
        
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Thickness", Range(0,1)) = 0
        _OutlineSoftness ("Outline Softness", Range(0,1)) = 0
        
        _UnderlayColor ("Border Color", Color) = (0,0,0,.5)
        _UnderlayOffsetX ("Border OffsetX", Range(-1,1)) = 0
        _UnderlayOffsetY ("Border OffsetY", Range(-1,1)) = 0
        _UnderlayDilate ("Border Dilate", Range(-1,1)) = 0
        _UnderlaySoftness ("Border Softness", Range(0,1)) = 0

        _WeightNormal ("Weight Normal", float) = 0
        _WeightBold ("Weight Bold", float) = .5

        _ScaleRatioA ("Scale Ratio A", Float) = 1
        _ScaleRatioB ("Scale Ratio B", Float) = 1
        _ScaleRatioC ("Scale Ratio C", Float) = 1
        _GradientScale ("Gradient Scale", Float) = 5.0
        _TextureWidth ("Texture Width", Float) = 512
        _TextureHeight ("Texture Height", Float) = 512

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        
        // Soft Mask Parameters
        [HideInInspector] _SoftMaskTex ("Soft Mask Texture", 2D) = "white" {}
        [HideInInspector] _SoftMask_Mode ("Mask Mode", Float) = 0
        [HideInInspector] _SoftMask_Rect ("Soft Mask Rect", Vector) = (0,0,0,0)

        [HideInInspector] _SoftMask_PR_Center ("PR Center", Vector) = (0,0,0,0)
        [HideInInspector] _SoftMask_PR_HalfSize ("PR Half Size", Vector) = (0,0,0,0)
        [HideInInspector] _SoftMask_PR_Radii ("PR Radii", Vector) = (0,0,0,0)
        [HideInInspector] _SoftMask_PR_Softness ("PR Softness", Float) = 0
        [HideInInspector] _SoftMask_PR_FillData ("PR Fill Data", Vector) = (0,0,0,0)

        [HideInInspector] _SoftMask_BorderData ("Border Data", Vector) = (0,0,0,0)
        [HideInInspector] _SoftMask_UVOuter ("UV Outer", Vector) = (0,0,1,1)
        [HideInInspector] _SoftMask_UVInner ("UV Inner", Vector) = (0,0,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        
        // TMP conventionally blends with premultiplied alpha 
        Blend One OneMinusSrcAlpha 
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "SoftMask.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local _ SOFTMASK_SLICED SOFTMASK_PROCEDURAL
            
            // TMP Multi-compiles
            #pragma multi_compile __ OUTLINE_ON
            #pragma multi_compile __ UNDERLAY_ON

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                float4 color     : COLOR; 
                float2 texcoord  : TEXCOORD0;
                float4 canvasPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // TMP Variables
            sampler2D _MainTex;
            float4 _FaceColor;
            float _FaceDilate;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineSoftness;
            float4 _UnderlayColor;
            float _UnderlayOffsetX;
            float _UnderlayOffsetY;
            float _UnderlayDilate;
            float _UnderlaySoftness;
            float _WeightNormal;
            float _WeightBold;

            float _ScaleRatioA;
            float _GradientScale;
            float _TextureWidth;
            float _TextureHeight;

            float4 _ClipRect;

            // Engine Polynomial Sync
            float _UIVertexColorAlwaysGammaSpace;

            inline float3 UI_GammaToLinear(float3 c)
            {
                float3 linearPath = c * 0.084971 - 0.000163;
                float3 gammaPath  = c * (c * (c * 0.265885 + 0.736584) - 0.009802) + 0.003197;
                float3 cmp = step(0.072549, c);
                return lerp(linearPath, gammaPath, cmp);
            }

            inline float4 UI_ColorSpaceSync(float4 c)
            {
            #if !defined(UNITY_COLORSPACE_GAMMA)
                if (_UIVertexColorAlwaysGammaSpace > 0.5) {
                    c.rgb = UI_GammaToLinear(c.rgb);
                }
            #endif
                return c;
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                OUT.canvasPos = v.vertex; 
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = UI_ColorSpaceSync(v.color);
                
                return OUT;
            }

            float4 frag(v2f IN) : SV_Target
            {
                // TextMeshPro Distance Field Rendering Pass
                float d = tex2D(_MainTex, IN.texcoord).a;
                
                // TMP SDF Base Math (Threshold is strictly 0.5)
                float scale = _GradientScale * _ScaleRatioA;
                if (scale < 0.001) scale = 5.0; // Fallback to prevent math errors
                
                float dist = (d - 0.5) * scale;
                float aa = max(fwidth(dist), 0.001);
                
                float faceDist = dist + _FaceDilate * scale;
                float alpha = smoothstep(-aa, aa, faceDist);
                float4 color = _FaceColor;

                #ifdef OUTLINE_ON
                    float outlineDist = dist + (_OutlineWidth + _FaceDilate) * scale;
                    float outlineAa = aa + _OutlineSoftness * scale;
                    float outlineAlpha = smoothstep(-outlineAa, outlineAa, outlineDist);
                    
                    color = lerp(_OutlineColor, color, alpha);
                    alpha = max(alpha, outlineAlpha);
                #endif
                
                #ifdef UNDERLAY_ON
                    // Standard TMP scaling for offsets: scale SDF units to UV space
                    float2 uvOffset = float2(_UnderlayOffsetX, _UnderlayOffsetY);
                    uvOffset.x *= (_GradientScale / max(_TextureWidth, 1.0));
                    uvOffset.y *= (_GradientScale / max(_TextureHeight, 1.0));

                    float underlayD = tex2D(_MainTex, IN.texcoord - uvOffset).a;
                    float underlayDist = (underlayD - 0.5) * scale;
                    float underlayFace = underlayDist + (_UnderlayDilate + _FaceDilate) * scale;
                    
                    float underlayAa = aa + _UnderlaySoftness * scale;
                    
                    // Clamping softness: Prevent bounding box artifact by ensuring smoothstep lower bound 
                    // doesn't push past the SDF's max negative distance (empty space boundary where d=0).
                    float maxAa = max(aa, (0.5 - (_UnderlayDilate + _FaceDilate)) * scale - 0.001);
                    float clampedAa = min(underlayAa, maxAa);

                    float underlayAlpha = smoothstep(-clampedAa, clampedAa, underlayFace);
                    
                    // Composite underlay
                    float finalAlpha = alpha + underlayAlpha * (1.0 - alpha);
                    color = (color * alpha + _UnderlayColor * underlayAlpha * (1.0 - alpha)) / max(finalAlpha, 0.0001);
                    alpha = finalAlpha;
                #endif

                color *= IN.color; // Vertex tint / Rich Text tags
                color.a *= alpha;
                
                // TMP Uses Premultiplied Alpha
                color.rgb *= color.a;


                // Soft Mask Rendering Pass
                color *= SoftMask_GetAlpha(IN.canvasPos);

                #ifdef UNITY_UI_CLIP_RECT
                color *= UnityGet2DClipping(IN.canvasPos.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}