using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Cue.Framework.UI;

namespace Cue.Framework.Rendering;

/// <summary>
/// An app's own drawing, recorded by the <see cref="Renderer"/> each frame before the UI (outside any rendering
/// pass): a 3D view, say, drawn into an image the UI then shows as a texture (<see cref="Batch.AddTexture"/>).
/// </summary>
public interface IPrePass
{
    /// <summary>
    /// Records this frame's drawing into <paramref name="cmd"/>. <paramref name="frameIndex"/> is the frame in flight's
    /// slot, from 0 to <see cref="Renderer.FRAMES_IN_FLIGHT"/> - 1, and the GPU has finished the last frame that used
    /// it. Images for the UI to show are given to <paramref name="ui"/> with <see cref="OverlayRenderer.SetTexture"/>.
    /// </summary>
    void Record(CommandBuffer cmd, int frameIndex, OverlayRenderer ui);
}
