using System.IO;
using UnityEngine;

public sealed class ImageResizer
{
    private readonly Material _bleedMat;
    private readonly Material _blurMat;
    private readonly Material _bicubicMat;

    public ImageResizer(Material bleedMat, Material blurMat, Material resizeMat)
    {
        _bleedMat = bleedMat;
        _blurMat = blurMat;
        _bicubicMat = resizeMat;
    }

    public bool ResizeImage(string inputPath, string outputPath, float scale, ref int newWidth, ref int newHeight, ref string errorMsg)
    {
        if (_bleedMat == null || _bleedMat == null || _bicubicMat == null)
        {
            errorMsg = "<color=red>ResizeImage: One or more materials are null</color>";
            return false;
        }

        if (!File.Exists(inputPath))
        {
            errorMsg = $"<color=red>ResizeImage: Input file not found: {inputPath}</color>";
            return false;
        }

        Texture2D source = LoadTexture(inputPath);
        if (source == null)
        {
            errorMsg = $"<color=red>ResizeImage: Failed to load texture at: {inputPath}</color>";
            return false;
        }

        source.wrapMode = TextureWrapMode.Clamp;
        source.wrapModeU = TextureWrapMode.Clamp;
        source.wrapModeV = TextureWrapMode.Clamp;

        newWidth = Mathf.FloorToInt(source.width * scale + 0.5f);
        newHeight = Mathf.FloorToInt(source.height * scale + 0.5f);

        Texture2D resized = ProgressiveResize(source, newWidth, newHeight);

        SaveTexture(resized, outputPath);

        UnityEngine.Object.DestroyImmediate(source);
        UnityEngine.Object.DestroyImmediate(resized);

        return true;
    }

    private Texture2D ProgressiveResize(Texture2D src, int finalW, int finalH)
    {
        Texture2D current = src;

        current.wrapMode = TextureWrapMode.Clamp;
        current.wrapModeU = TextureWrapMode.Clamp;
        current.wrapModeV = TextureWrapMode.Clamp;

        // Half only while next‐half is still at least twice the final size
        while (current.width / 2 >= finalW * 2 || current.height / 2 >= finalH * 2)
        {
            int w = current.width / 2;
            int h = current.height / 2;

            RenderTexture bleedRt = RenderTexture.GetTemporary(current.width, current.height, 0, RenderTextureFormat.Default);
            Graphics.Blit(current, bleedRt, _bleedMat);

            var next = PreBlurAndResize(bleedRt, w, h);

            RenderTexture.ReleaseTemporary(bleedRt);

            if (current != src)
            {
                UnityEngine.Object.DestroyImmediate(current);
            }

            current = next;

            current.wrapMode = TextureWrapMode.Clamp;
            current.wrapModeU = TextureWrapMode.Clamp;
            current.wrapModeV = TextureWrapMode.Clamp;
        }

        // Single final step
        if (current.width != finalW || current.height != finalH)
        {
            RenderTexture bleedRt = RenderTexture.GetTemporary(current.width, current.height, 0, RenderTextureFormat.Default);
            Graphics.Blit(current, bleedRt, _bleedMat);

            var last = PreBlurAndResize(bleedRt, finalW, finalH);

            RenderTexture.ReleaseTemporary(bleedRt);

            if (current != src)
            {
                UnityEngine.Object.DestroyImmediate(current);
            }

            current = last;
        }

        return current;
    }

    private static Texture2D LoadTexture(string path)
    {
        byte[] data = File.ReadAllBytes(path);

        // Force sRGB interpretation regardless of project color space
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false);
        tex.LoadImage(data, markNonReadable: false);

        tex.filterMode = FilterMode.Bilinear;
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        return tex;
    }

    private Texture2D PreBlurAndResize(RenderTexture source, int outW, int outH)
    {
        // Prepare descriptors
        var desc = new RenderTextureDescriptor(source.width, source.height,
            RenderTextureFormat.ARGB32, 0)
        {
            sRGB = true,
            useMipMap = false,
            autoGenerateMips = false
        };

        // When creating RTs:
        desc.colorFormat = RenderTextureFormat.ARGB32;
        desc.useMipMap = false;
        desc.autoGenerateMips = false;

        // Create two temp RTs for blur
        var rtH = RenderTexture.GetTemporary(desc);
        rtH.wrapMode = TextureWrapMode.Clamp;
        var rtV = RenderTexture.GetTemporary(desc);
        rtV.wrapMode = TextureWrapMode.Clamp;

        // Shader expects _Direction = (1,0) or (0,1)
        _blurMat.SetVector("_Direction", Vector2.right);
        Graphics.Blit(source, rtH, _blurMat);

        _blurMat.SetVector("_Direction", Vector2.up);
        Graphics.Blit(rtH, rtV, _blurMat);

        // Now bicubic-resize the blurred RT into final RT
        desc.width = outW;
        desc.height = outH;
        var rtFinal = RenderTexture.GetTemporary(desc);

        Graphics.Blit(rtV, rtFinal, _bicubicMat);

        RenderTexture.active = rtFinal;
        var result = new Texture2D(outW, outH, TextureFormat.RGBA32, mipChain: false, linear: false);
        result.ReadPixels(new Rect(0, 0, outW, outH), 0, 0);
        result.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        // Cleanup
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rtH);
        RenderTexture.ReleaseTemporary(rtV);
        RenderTexture.ReleaseTemporary(rtFinal);

        return result;
    }

    private static void SaveTexture(Texture2D tex, string outputPath)
    {
        byte[] pngData = tex.EncodeToPNG();
        File.WriteAllBytes(outputPath, pngData);
    }
}
