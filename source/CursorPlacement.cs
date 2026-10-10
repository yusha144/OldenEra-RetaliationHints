namespace OldenEra.RetaliationHints;

internal readonly record struct CursorBox(float X, float Y, float Width, float Height)
{
    internal bool Valid => Width > 0 && Height > 0 && float.IsFinite(X + Y + Width + Height);
}

internal static class CursorPlacement
{
    // Screen coordinates, origin at bottom left. Keep clear of the attack cursor;
    // flip to the other side at viewport edges rather than covering its hotspot.
    internal static CursorBox Place(float screenWidth, float screenHeight, float mouseX, float mouseY,
        float width, float height, float scale)
    {
        if (!float.IsFinite(screenWidth + screenHeight + mouseX + mouseY + width + height + scale)
            || scale <= 0 || width <= 0 || height <= 0 || mouseX < 0 || mouseY < 0
            || mouseX > screenWidth || mouseY > screenHeight) return default;
        float margin = 6 * scale, gapX = 28 * scale, gapY = 24 * scale;
        if (screenWidth <= 2 * margin || screenHeight <= 2 * margin) return default;
        width = Math.Min(width, screenWidth - 2 * margin);
        height = Math.Min(height, screenHeight - 2 * margin);
        float x = mouseX + gapX, y = mouseY - gapY - height;
        if (x + width > screenWidth - margin) x = mouseX - gapX - width;
        if (y < margin) y = mouseY + gapY;
        x = Math.Clamp(x, margin, screenWidth - margin - width);
        y = Math.Clamp(y, margin, screenHeight - margin - height);
        return new(x, y, width, height);
    }
}
