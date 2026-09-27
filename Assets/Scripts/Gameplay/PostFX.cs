using UnityEngine;

namespace ArenaShooter.Gameplay
{
    /// <summary>
    /// Camera post effects for the built-in pipeline: soft bloom on bright things (bolts, glows,
    /// the sun), filmic (ACES) tone mapping, a touch of saturation/contrast and a light vignette.
    /// Uses Resources/Shaders/PostFX.shader; if that can't run it just copies the image.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
        [Header("Bloom")]
        public float bloomThreshold = 1f;
        [Range(0f, 1f)] public float bloomKnee = 0.5f;
        public float bloomIntensity = 0.55f;
        [Range(1, 8)] public int bloomIterations = 6;

        [Header("Color")]
        public float exposure = 0.85f;
        public float saturation = 1.08f;
        public float contrast = 1.06f;
        [Range(0f, 1f)] public float vignette = 0.28f;

        private const int PrefilterPass = 0, DownPass = 1, UpPass = 2, FinalPass = 3;
        private static Shader _shader;
        private static bool _shaderMissing;
        private Material _material;
        private readonly RenderTexture[] _chain = new RenderTexture[8];

        private static Shader LoadShader()
        {
            if (_shader == null && !_shaderMissing)
            {
                _shader = Resources.Load<Shader>("Shaders/PostFX");
                if (_shader == null) _shader = Shader.Find("Hidden/SpaceGrunts/PostFX");
                _shaderMissing = _shader == null || !_shader.isSupported;
                if (_shaderMissing) Debug.LogWarning("PostFX shader unavailable; running without post effects.");
            }
            return _shaderMissing ? null : _shader;
        }

        private void OnDisable()
        {
            if (_material != null) Destroy(_material);
            _material = null;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_material == null)
            {
                var shader = LoadShader();
                if (shader == null)
                {
                    Graphics.Blit(source, destination);
                    return;
                }
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            _material.SetFloat("_Threshold", bloomThreshold);
            _material.SetFloat("_Knee", Mathf.Max(0.0001f, bloomThreshold * bloomKnee));
            _material.SetFloat("_Intensity", bloomIntensity);
            _material.SetFloat("_Exposure", exposure);
            _material.SetFloat("_Saturation", saturation);
            _material.SetFloat("_Contrast", contrast);
            _material.SetFloat("_Vignette", vignette);

            // Bloom: bright-pass at half size, blur down a mip chain, then add it back up.
            int width = Mathf.Max(1, source.width / 2), height = Mathf.Max(1, source.height / 2);
            int count = 0;
            RenderTexture current = null;
            for (int i = 0; i < Mathf.Min(bloomIterations, _chain.Length); i++)
            {
                if (width < 2 || height < 2) break;
                var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear);
                rt.filterMode = FilterMode.Bilinear;
                Graphics.Blit(i == 0 ? source : current, rt, _material, i == 0 ? PrefilterPass : DownPass);
                _chain[count++] = rt;
                current = rt;
                width /= 2;
                height /= 2;
            }
            for (int i = count - 2; i >= 0; i--)
                Graphics.Blit(_chain[i + 1], _chain[i], _material, UpPass);

            _material.SetTexture("_BloomTex", count > 0 ? _chain[0] : (Texture)Texture2D.blackTexture);
            Graphics.Blit(source, destination, _material, FinalPass);

            for (int i = 0; i < count; i++)
            {
                RenderTexture.ReleaseTemporary(_chain[i]);
                _chain[i] = null;
            }
        }
    }
}
