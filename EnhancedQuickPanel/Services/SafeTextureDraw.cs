using Dalamud.Interface.Textures.TextureWraps;

namespace EnhancedQuickPanel.Services;

/// <summary>Draws a texture with guards against invalid or disposed wraps.</summary>
internal static class SafeTextureDraw
{
    public static bool TryAddImage(
        ImDrawListPtr drawList,
        IDalamudTextureWrap? texture,
        Vector2 min,
        Vector2 max,
        uint tint)
    {
        if (texture == null || texture.Handle == 0)
            return false;

        try
        {
            drawList.AddImage(texture.Handle, min, max, Vector2.Zero, Vector2.One, tint);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public static bool TryAddImage(
        ImDrawListPtr drawList,
        IDalamudTextureWrap? texture,
        Vector2 min,
        Vector2 max,
        Vector2 uv0,
        Vector2 uv1,
        uint tint)
    {
        if (texture == null || texture.Handle == 0)
            return false;

        try
        {
            drawList.AddImage(texture.Handle, min, max, uv0, uv1, tint);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}

