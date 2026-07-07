using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EnhancedQuickPanel.Services;

/// <summary>Reads slot icon paths and layout from the native quick panel UI addon.</summary>
internal static unsafe class NativeQuickPanelUiReader
{
    private const int MaxNodesToVisit = 512;
    private const uint MaxLoadableIconId = ResolvedSlotIcon.MaxResolvableIconId;

    public static bool TryGetSlotIcon(int slotIndex, out ResolvedSlotIcon icon)
    {
        icon = ResolvedSlotIcon.Empty;
        return TryGetDragDropAt(slotIndex, out var dragDrop)
            && TryReadDragDropIcon((AtkComponentDragDrop*)dragDrop, out icon);
    }

    public static bool TryGetSlotOverlay(int slotIndex, out SlotOverlayInfo overlay)
    {
        overlay = SlotOverlayInfo.None;
        if (!TryGetDragDropAt(slotIndex, out var dragDropPtr))
            return false;

        var dragDrop = (AtkComponentDragDrop*)dragDropPtr;
        var componentIcon = dragDrop->AtkComponentIcon;
        if (componentIcon == null)
            return false;

        var showQuantity = TryReadQuantity(dragDrop, componentIcon, out var quantity);
        var showMacro = componentIcon->Flags.HasFlag(IconComponentFlags.IsMacro);
        if (!showQuantity && !showMacro)
            return false;

        var isGrayedOut = TryReadIconGrayedOut(componentIcon);
        overlay = new SlotOverlayInfo(showQuantity, quantity, isGrayedOut, showMacro);
        return true;
    }

    public static bool TryGetSlotIconImagePath(int slotIndex, out string texturePath)
    {
        texturePath = string.Empty;
        if (!TryGetDragDropAt(slotIndex, out var dragDropPtr))
            return false;

        var componentIcon = ((AtkComponentDragDrop*)dragDropPtr)->AtkComponentIcon;
        if (componentIcon == null)
            return false;

        return TryGetIconImageTexturePath(componentIcon, out texturePath);
    }

    public static bool TryGetSlotFrameSlice(int slotIndex, out SlotTextureSlice slice)
    {
        slice = default;
        if (!TryGetDragDropAt(slotIndex, out var dragDropPtr))
            return false;

        var componentIcon = ((AtkComponentDragDrop*)dragDropPtr)->AtkComponentIcon;
        if (componentIcon == null || componentIcon->FrameIcon == null)
            return false;

        return SlotImageResolver.TryGetImageNodeSlice(componentIcon->FrameIcon, out slice);
    }

    public static bool TryGetSlotIconSlice(int slotIndex, out SlotTextureSlice slice)
    {
        slice = default;
        if (!TryGetDragDropAt(slotIndex, out var dragDropPtr))
            return false;

        var componentIcon = ((AtkComponentDragDrop*)dragDropPtr)->AtkComponentIcon;
        if (componentIcon == null || componentIcon->IconImage == null)
            return false;

        return SlotImageResolver.TryGetImageNodeSlice(componentIcon->IconImage, out slice);
    }

    internal static bool TryGetImageNodeTexturePath(AtkImageNode* imageNode, out string texturePath)
    {
        texturePath = string.Empty;
        if (imageNode == null)
            return false;

        if (imageNode->PartsList == null || imageNode->PartId >= imageNode->PartsList->PartCount)
            return false;

        var part = imageNode->PartsList->Parts[imageNode->PartId];
        if (part.UldAsset == null)
            return false;

        var atkTexture = part.UldAsset->AtkTexture;
        if (atkTexture.TextureType != TextureType.Resource
            || atkTexture.Resource == null
            || atkTexture.Resource->TexFileResourceHandle == null)
            return false;

        texturePath = atkTexture.Resource->TexFileResourceHandle->FileName.ToString();
        return !string.IsNullOrWhiteSpace(texturePath);
    }

    internal static nint[] CollectDragDropPointers(AtkUnitBase* addon)
    {
        var entries = new List<DragDropEntry>();
        var visited = 0;

        if (addon->RootNode != null)
            VisitNode(addon->RootNode, entries, ref visited);

        return entries
            .OrderBy(entry => entry.SortY)
            .ThenBy(entry => entry.SortX)
            .Take(Configuration.SlotsPerPage)
            .Select(entry => entry.DragDrop)
            .ToArray();
    }

    internal static bool TryFindDragDropComponent(
        AtkUnitBase* addon,
        out AtkComponentDragDrop* dragDrop)
    {
        dragDrop = null;
        if (addon == null)
            return false;

        foreach (var pointer in CollectDragDropPointers(addon))
        {
            var candidate = (AtkComponentDragDrop*)pointer;
            if (!TryReadDragDropIcon(candidate, out _))
                continue;

            dragDrop = candidate;
            return true;
        }

        return false;
    }

    internal static bool TryReadDragDropAppearance(
        AtkComponentDragDrop* dragDrop,
        out ResolvedSlotIcon icon,
        out int? quantity)
    {
        icon = ResolvedSlotIcon.Empty;
        quantity = null;

        if (!TryReadDragDropIcon(dragDrop, out icon))
            return false;

        var componentIcon = dragDrop->AtkComponentIcon;
        if (componentIcon != null && TryReadQuantity(dragDrop, componentIcon, out var parsedQuantity))
            quantity = parsedQuantity;

        return true;
    }

    internal static bool TryReadDragSourceAppearance(
        AtkComponentDragDrop* dragDrop,
        out ResolvedSlotIcon icon,
        out int? quantity)
    {
        icon = ResolvedSlotIcon.Empty;
        quantity = null;

        if (dragDrop == null)
            return false;

        if (TryReadDragDropAppearance(dragDrop, out icon, out quantity) && icon.IconId > 0)
            return true;

        var componentIcon = dragDrop->AtkComponentIcon;
        if (componentIcon != null && componentIcon->IconId > 0)
        {
            var isHq = componentIcon->IconSubFolder == IconSubFolder.HighQuality;
            icon = new ResolvedSlotIcon(componentIcon->IconId, isHq);
            if (TryReadQuantity(dragDrop, componentIcon, out var parsedQuantity))
                quantity = parsedQuantity;

            return true;
        }

        var fallbackIconId = dragDrop->GetIconId();
        if (fallbackIconId <= 0)
            return false;

        icon = new ResolvedSlotIcon((uint)fallbackIconId, false);
        return true;
    }

    private static bool TryGetDragDropAt(int slotIndex, out nint dragDrop) =>
        QuickPanelUiCache.TryGetDragDropAt(slotIndex, out dragDrop);

    /// <summary>A native drag-drop node with its sort position.</summary>
    private struct DragDropEntry(float sortY, float sortX, nint dragDrop)
    {
        public float SortY = sortY;
        public float SortX = sortX;
        public nint DragDrop = dragDrop;
    }

    private static void VisitNode(AtkResNode* node, List<DragDropEntry> entries, ref int visited)
    {
        try
        {
            for (var current = node; current != null && visited < MaxNodesToVisit; current = current->NextSiblingNode)
            {
                visited++;

                if (current->Type == NodeType.Component && current->IsVisible())
                    TryCollectDragDrop(current, entries);

                if (current->ChildNode != null)
                    VisitNode(current->ChildNode, entries, ref visited);
            }
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"[EQP] Addon node visit failed: {ex.Message}");
        }
    }

    private static void TryCollectDragDrop(AtkResNode* node, List<DragDropEntry> entries)
    {
        var componentNode = node->GetAsAtkComponentNode();
        if (componentNode == null)
            return;

        var component = componentNode->Component;
        if (component == null || component->GetComponentType() != ComponentType.DragDrop)
            return;

        var dragDrop = (AtkComponentDragDrop*)component;
        if (!TryReadDragDropIcon(dragDrop, out _))
            return;

        var resNode = component->AtkResNode != null ? component->AtkResNode : node;
        entries.Add(new DragDropEntry(resNode->Y, resNode->X, (nint)dragDrop));
    }

    private static bool TryReadDragDropIcon(AtkComponentDragDrop* dragDrop, out ResolvedSlotIcon icon)
    {
        icon = ResolvedSlotIcon.Empty;
        if (dragDrop == null)
            return false;

        var componentIcon = dragDrop->AtkComponentIcon;
        if (componentIcon != null && TryReadComponentIcon(componentIcon, out icon))
            return true;

        var iconId = dragDrop->GetIconId();
        if (!IsLoadableIcon((uint)iconId))
            return false;

        icon = new ResolvedSlotIcon((uint)iconId, false);
        return true;
    }

    private static bool TryReadComponentIcon(AtkComponentIcon* componentIcon, out ResolvedSlotIcon icon)
    {
        icon = ResolvedSlotIcon.Empty;

        var iconId = componentIcon->IconId;
        if (!IsLoadableIcon(iconId))
            return false;

        var isHq = componentIcon->IconSubFolder == IconSubFolder.HighQuality;
        icon = new ResolvedSlotIcon(iconId, isHq);
        return true;
    }

    private static bool TryReadQuantity(
        AtkComponentDragDrop* dragDrop,
        AtkComponentIcon* componentIcon,
        out int quantity)
    {
        quantity = 0;

        var quantityText = dragDrop->GetQuantityText().ToString();
        if (TryParseQuantityText(quantityText, out quantity))
            return true;

        var quantityNode = componentIcon->QuantityText;
        if (quantityNode != null && quantityNode->IsVisible())
        {
            quantityText = quantityNode->NodeText.ToString();
            if (TryParseQuantityText(quantityText, out quantity))
                return true;
        }

        return false;
    }

    private static bool TryReadIconGrayedOut(AtkComponentIcon* componentIcon)
    {
        var imageNode = componentIcon->IconImage;
        if (imageNode == null)
            return false;

        // SetIconImageDisableState sets MultiplyRGB to 50 when unavailable.
        return imageNode->MultiplyRed <= 50
            && imageNode->MultiplyGreen <= 50
            && imageNode->MultiplyBlue <= 50;
    }

    private static bool TryParseQuantityText(string? quantityText, out int quantity)
    {
        quantity = 0;
        if (string.IsNullOrWhiteSpace(quantityText))
            return false;

        quantityText = quantityText.Trim();
        if (quantityText.StartsWith('x') || quantityText.StartsWith('X'))
            quantityText = quantityText[1..].Trim();

        return int.TryParse(quantityText, out quantity);
    }

    private static bool IsLoadableIcon(uint iconId) =>
        iconId is > 0 and <= MaxLoadableIconId;

    private static bool TryGetIconImageTexturePath(AtkComponentIcon* componentIcon, out string texturePath)
    {
        texturePath = string.Empty;
        if (componentIcon == null)
            return false;

        var imageNode = componentIcon->IconImage;
        return imageNode != null && TryGetImageNodeTexturePath(imageNode, out texturePath);
    }
}

