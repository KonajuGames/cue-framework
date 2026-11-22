using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Brutal.VulkanApi.Vma;
using Cue.Framework.Rendering;

namespace Cue.Framework.UI;

/// <summary>
/// Draws a <see cref="Root"/> into the current rendering pass: one alpha-blended pipeline for both text and solid
/// rectangles, reading the font atlas, with vertices rebuilt every frame.
/// </summary>
public sealed unsafe class OverlayRenderer : IDisposable
{
    struct PushConstants
    {
        public float2 ScreenSize;
    }

    /// <summary>
    /// Per frame in flight, since the GPU may still be reading the previous frame's vertices.
    /// </summary>
    sealed class FrameVertices
    {
        public GpuBuffer? Buffer;
        public int Capacity;
    }

    readonly GpuContext _gpu;
    readonly DeviceEx _device;
    readonly Batch _batch;

    readonly GpuImage _atlasImage;
    readonly VkSampler _sampler;
    readonly VkDescriptorSetLayout _descriptorSetLayout;
    readonly VkDescriptorPool _descriptorPool;
    readonly VkDescriptorSet _descriptorSet;
    readonly VkPipelineLayout _pipelineLayout;
    readonly VkPipeline _pipeline;

    readonly FrameVertices[] _frames;
    readonly int _framesInFlight;

    /// <summary>
    /// Builds <paramref name="atlas"/> if it isn't already; add every font before this.
    /// </summary>
    public OverlayRenderer(GpuContext gpu, FontAtlas atlas, VkFormat colorFormat, int framesInFlight)
    {
        _gpu = gpu;
        _device = gpu.Device;

        atlas.Build();
        _batch = new Batch(atlas);
        _atlasImage = UploadAtlas(gpu, atlas);
        _sampler = _device.CreateSampler(Presets.Sampler.SamplerLinearClamped, null);

        var samplerBinding = new VkDescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = VkDescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = VkShaderStageFlags.FragmentBit,
        };
        _descriptorSetLayout = _device.CreateDescriptorSetLayout(
            new VkDescriptorSetLayoutCreateInfo { BindingCount = 1, Bindings = &samplerBinding },
            null
        );

        // The atlas's set and the other textures' (see SetTexture).
        var poolSize = new VkDescriptorPoolSize { Type = VkDescriptorType.CombinedImageSampler, DescriptorCount = MAX_TEXTURES };
        _descriptorPool = _device.CreateDescriptorPool(
            new VkDescriptorPoolCreateInfo { MaxSets = MAX_TEXTURES, PoolSizeCount = 1, PoolSizes = &poolSize },
            null
        );

        _descriptorSet = AllocateSet();
        WriteSet(_descriptorSet, _atlasImage.View);

        _pipelineLayout = _device.CreatePipelineLayout(
            [_descriptorSetLayout],
            [
                new VkPushConstantRange
                {
                    StageFlags = VkShaderStageFlags.VertexBit,
                    Size = ByteSize.Of<PushConstants>(),
                },
            ],
            null
        );
        _pipeline = CreatePipeline(colorFormat);

        _framesInFlight = framesInFlight;
        _frames = new FrameVertices[framesInFlight];
        for (var i = 0; i < framesInFlight; i++)
            _frames[i] = new FrameVertices();
    }

    /// <summary>
    /// How many textures can be drawn from at once, the atlas among them: those given with <see cref="SetTexture"/>
    /// and the images made with <see cref="AddImage"/>.
    /// </summary>
    public const int MAX_TEXTURES = 64;

    readonly Dictionary<int, VkDescriptorSet> _textures = [];

    // Sets allocated so far, and those given back by images removed, once the GPU's finished with them.
    int _setsAllocated;
    readonly Stack<VkDescriptorSet> _spareSets = [];

    VkDescriptorSet AllocateSet()
    {
        if (_spareSets.TryPop(out var spare))
            return spare;
        if (_setsAllocated == MAX_TEXTURES)
            throw new InvalidOperationException($"The UI can only draw from {MAX_TEXTURES} textures at once; remove images no longer shown");
        _setsAllocated++;
        var setLayout = _descriptorSetLayout;
        Span<VkDescriptorSet> sets = stackalloc VkDescriptorSet[1];
        _device.AllocateDescriptorSets(
            new VkDescriptorSetAllocateInfo
            {
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = 1,
                SetLayouts = &setLayout,
            },
            sets
        );
        return sets[0];
    }

    void WriteSet(VkDescriptorSet set, VkImageView view)
    {
        var imageInfo = new VkDescriptorImageInfo
        {
            Sampler = _sampler,
            ImageView = view,
            ImageLayout = VkImageLayout.ShaderReadOnlyOptimal,
        };
        _device.UpdateDescriptorSets(
            [
                new VkWriteDescriptorSet
                {
                    DstSet = set,
                    DstBinding = 0,
                    DescriptorCount = 1,
                    DescriptorType = VkDescriptorType.CombinedImageSampler,
                    ImageInfo = &imageInfo,
                },
            ],
            []
        );
    }

    /// <summary>
    /// Makes <paramref name="view"/> texture <paramref name="id"/> (1 and up), for <see cref="Batch.AddTexture"/>; it
    /// must be in the shader-read layout whenever the UI's drawn. Call while the GPU isn't using the old one (after a
    /// wait for idle).
    /// </summary>
    public void SetTexture(int id, VkImageView view)
    {
        if (id is < 1 or >= FIRST_IMAGE)
            throw new ArgumentOutOfRangeException(nameof(id), id, $"Textures given are numbered from 1 to {FIRST_IMAGE - 1}");
        if (!_textures.TryGetValue(id, out var set))
            _textures[id] = set = AllocateSet();
        WriteSet(set, view);
    }

    /// <summary>
    /// The number the first image made with <see cref="AddImage"/> gets; those given with <see cref="SetTexture"/> are
    /// below it.
    /// </summary>
    public const int FIRST_IMAGE = 1 << 16;

    /// <summary>
    /// An image made while running (<see cref="AddImage"/>), and its pixels until they're copied to it.
    /// </summary>
    sealed class AddedImage(GpuImage image, VkDescriptorSet set, GpuBuffer staging)
    {
        public readonly GpuImage Image = image;
        public readonly VkDescriptorSet Set = set;
        public GpuBuffer? Staging = staging;
    }

    int _nextImage = FIRST_IMAGE;
    readonly Dictionary<int, AddedImage> _images = [];

    /// <summary>
    /// Images waiting to be copied to the GPU at the start of the next frame (see <see cref="BeginFrame"/>).
    /// </summary>
    readonly List<AddedImage> _uploads = [];

    /// <summary>
    /// What's to be freed once the GPU can no longer be using it: when frame FreeAt begins (see
    /// <see cref="BeginFrame"/>).
    /// </summary>
    readonly List<(long FreeAt, Action Free)> _retired = [];

    /// <summary>
    /// How many frames have begun; each frame's number is the count before it.
    /// </summary>
    long _frame;

    /// <summary>
    /// How many images made with <see cref="AddImage"/> are held on the GPU, those removed but not yet freed among
    /// them; for checking that an app gives back what it's finished with.
    /// </summary>
    public int ImagesHeld => _images.Count + _retiredImages;

    int _retiredImages;

    /// <summary>
    /// Makes an image from <paramref name="rgba"/>, rows of sRGB pixels from the top with alpha (not premultiplied),
    /// to draw with <see cref="Batch.AddTexture"/> by the number returned: a picture that isn't known until the app's
    /// running, such as a song's cover. It's copied to the GPU at the start of the next frame, without waiting. Remove
    /// it with <see cref="RemoveImage"/> once it's no longer shown; until then it holds GPU memory, and one of the
    /// <see cref="MAX_TEXTURES"/> textures there can be.
    /// </summary>
    public int AddImage(int width, int height, ReadOnlySpan<byte> rgba)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (rgba.Length != width * height * 4)
            throw new ArgumentException($"A {width}x{height} image is {width * height * 4} bytes of RGBA, not {rgba.Length}", nameof(rgba));

        var set = AllocateSet();
        var image = new GpuImage(
            _gpu,
            VkFormat.R8G8B8A8SRGB,
            new VkExtent2D(width, height),
            1,
            VkImageUsageFlags.SampledBit | VkImageUsageFlags.TransferDstBit,
            VkImageAspectFlags.ColorBit
        );
        // No frame uses the set yet, so it can be written now; nothing's drawn from it before the copy.
        WriteSet(set, image.View);
        var added = new AddedImage(image, set, _gpu.CreateStagingBuffer(rgba));

        var id = _nextImage++;
        _images[id] = added;
        _textures[id] = set;
        _uploads.Add(added);
        return id;
    }

    /// <summary>
    /// Stops drawing image <paramref name="id"/> (from <see cref="AddImage"/>) and frees it once the frames still on
    /// the GPU are done with it. Does nothing if there's no such image, such as one already removed.
    /// </summary>
    public void RemoveImage(int id)
    {
        if (!_images.Remove(id, out var removed))
            return;
        _textures.Remove(id);

        // Never copied, so never drawn: free it now.
        if (_uploads.Remove(removed))
        {
            Free();
            return;
        }

        // The last frame begun may have drawn it.
        _retiredImages++;
        _retired.Add((_frame - 1 + _framesInFlight, () =>
        {
            _retiredImages--;
            Free();
        }));

        void Free()
        {
            removed.Staging?.Dispose();
            removed.Image.Dispose();
            _spareSets.Push(removed.Set);
        }
    }

    /// <summary>
    /// Starts a frame: frees what the GPU's finished with and records the copies of images added since the last frame
    /// into <paramref name="cmd"/>. Call once a frame, outside any rendering pass and before <see cref="Record"/>,
    /// once the GPU's finished the last frame that used this frame's slot (as <see cref="Renderer"/> does).
    /// </summary>
    public void BeginFrame(CommandBuffer cmd)
    {
        var frame = _frame++;

        // This frame's slot was last used framesInFlight frames ago, so that frame's finished, and every frame before
        // it (a queue's work finishes in the order it was sent).
        for (var i = _retired.Count - 1; i >= 0; i--)
        {
            if (_retired[i].FreeAt > frame)
                continue;
            var free = _retired[i].Free;
            _retired.RemoveAt(i);
            free();
        }

        foreach (var upload in _uploads)
        {
            var staging = upload.Staging!;
            RecordUpload(cmd, upload.Image.Image, upload.Image.Extent, staging);
            // The pixels are read while this frame runs, so they go once it's done.
            upload.Staging = null;
            _retired.Add((frame + _framesInFlight, staging.Dispose));
        }
        _uploads.Clear();
    }

    static GpuImage UploadAtlas(GpuContext gpu, FontAtlas atlas)
    {
        var image = new GpuImage(
            gpu,
            // sRGB, so colour images are read back in linear light like everything else; alpha stays linear.
            VkFormat.R8G8B8A8SRGB,
            new VkExtent2D(atlas.Width, atlas.Height),
            1,
            VkImageUsageFlags.SampledBit | VkImageUsageFlags.TransferDstBit,
            VkImageAspectFlags.ColorBit
        );

        using var staging = gpu.CreateStagingBuffer<byte>(atlas.Pixels);
        gpu.SubmitAndWait(cmd => RecordUpload(cmd, image.Image, image.Extent, staging));
        return image;
    }

    /// <summary>
    /// Copies <paramref name="staging"/>'s pixels into all of <paramref name="image"/>, leaving it ready for the
    /// fragment shader to read.
    /// </summary>
    static void RecordUpload(CommandBuffer cmd, VkImage image, VkExtent2D extent, GpuBuffer staging)
    {
        {
            cmd.TransitionImage(
                image,
                VkImageAspectFlags.ColorBit,
                VkImageLayout.Undefined,
                VkImageLayout.TransferDstOptimal,
                VkPipelineStageFlags.TopOfPipeBit,
                VkAccessFlags.None,
                VkPipelineStageFlags.TransferBit,
                VkAccessFlags.TransferWriteBit
            );
            cmd.CopyBufferToImage(
                staging.Buffer,
                image,
                VkImageLayout.TransferDstOptimal,
                [
                    new VkBufferImageCopy
                    {
                        ImageSubresource = Subresources.Single(VkImageAspectFlags.ColorBit).ToLayers(),
                        ImageExtent = new VkExtent3D
                        {
                            Width = extent.Width,
                            Height = extent.Height,
                            Depth = 1,
                        },
                    },
                ]
            );
            cmd.TransitionImage(
                image,
                VkImageAspectFlags.ColorBit,
                VkImageLayout.TransferDstOptimal,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags.TransferBit,
                VkAccessFlags.TransferWriteBit,
                VkPipelineStageFlags.FragmentShaderBit,
                VkAccessFlags.ShaderReadBit
            );
        }
    }

    VkPipeline CreatePipeline(VkFormat colorFormat)
    {
        using var shaderStages = ShaderLoader.Load(_device, "ui.vert", "ui.frag");
        var vertexInput = VertexInput.For<Vertex>();

        var viewportState = new VkPipelineViewportStateCreateInfo { ViewportCount = 1, ScissorCount = 1 };
        var dynamicStates = stackalloc VkDynamicState[] { VkDynamicState.Viewport, VkDynamicState.Scissor };
        var dynamicState = new VkPipelineDynamicStateCreateInfo
        {
            DynamicStateCount = 2,
            DynamicStates = dynamicStates,
        };

        var rendering = new VkPipelineRenderingCreateInfo { ColorAttachmentCount = 1, ColorAttachmentFormats = &colorFormat };

        return _device.CreateGraphicsPipeline(
            VkPipelineCache.NullHandle,
            new VkGraphicsPipelineCreateInfo
            {
                Next = &rendering,
                StageCount = shaderStages.Count,
                Stages = shaderStages,
                VertexInputState = vertexInput,
                InputAssemblyState = Presets.InputAssembly.TriangleList,
                ViewportState = &viewportState,
                RasterizationState = Presets.Rasterization.Fill.CullNone,
                MultisampleState = Presets.Multisample.None,
                DepthStencilState = Presets.DepthStencil.NoDepthTest,
                ColorBlendState = Presets.BlendState.BlendColorAlpha,
                DynamicState = &dynamicState,
                Layout = _pipelineLayout,
            },
            null
        );
    }

    /// <summary>
    /// Draws <paramref name="ui"/>, which must already be laid out, into the current rendering pass. The caller must
    /// have waited for <paramref name="frameIndex"/>'s previous use to finish on the GPU.
    /// </summary>
    public void Record(CommandBuffer cmd, int frameIndex, VkExtent2D extent, Root ui)
    {
        var screen = new float2(extent.Width, extent.Height);

        _batch.Clear();
        _batch.Scale = ui.Scale;
        ui.Draw(_batch);

        var vertices = _batch.Vertices;
        var vertexCount = vertices.Length;
        if (vertexCount == 0)
            return;

        var frame = _frames[frameIndex];
        if (frame.Buffer is null || frame.Capacity < vertexCount)
        {
            // Safe to free: this frame slot's last submission has finished.
            frame.Buffer?.Dispose();
            frame.Capacity = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(vertexCount, 1024));
            frame.Buffer = GpuBuffer.Create(
                _gpu.Allocator,
                ByteSize.Of<Vertex>() * frame.Capacity,
                VkBufferUsageFlags.VertexBufferBit,
                VmaAllocationCreateFlags.HostAccessSequentialWriteBit | VmaAllocationCreateFlags.MappedBit
            );
        }

        vertices.CopyTo(new Span<Vertex>((void*)frame.Buffer.Info.PMappedData, vertexCount));
        _gpu.Allocator.FlushAllocation(frame.Buffer.Allocation, ByteSize.Zero, ByteSize.Of<Vertex>() * vertexCount);

        // An unflipped viewport: UI coordinates already point down, as Vulkan's clip space does.
        cmd.SetViewport(
            0,
            [
                new VkViewport
                {
                    Width = extent.Width,
                    Height = extent.Height,
                    MaxDepth = 1,
                },
            ]
        );
        cmd.SetScissor(0, [new VkRect2D(extent)]);

        cmd.BindPipeline(VkPipelineBindPoint.Graphics, _pipeline);
        cmd.PushConstants(
            _pipelineLayout,
            VkShaderStageFlags.VertexBit,
            ByteSize.Zero,
            new PushConstants { ScreenSize = screen }
        );
        cmd.BindVertexBuffers(0, [frame.Buffer.Buffer], [new ByteSize64(0)]);
        // In runs, each from its texture: the atlas, but where an image of the renderer's own is drawn.
        var start = 0;
        var texture = 0;
        foreach (var (at, next) in _batch.TextureChanges)
        {
            Run(start, at, texture);
            (start, texture) = (at, next);
        }
        Run(start, vertexCount, texture);

        void Run(int from, int to, int id)
        {
            if (to <= from)
                return;
            VkDescriptorSet set;
            if (id == 0)
                set = _descriptorSet;
            else if (!_textures.TryGetValue(id, out set))
                return;
            cmd.BindDescriptorSets(VkPipelineBindPoint.Graphics, _pipelineLayout, 0, [set], []);
            cmd.Draw(to - from, 1, from, 0);
        }
    }

    /// <summary>
    /// Frees everything, the images added among it; the GPU must have finished with it all (after a wait for idle).
    /// </summary>
    public void Dispose()
    {
        foreach (var (_, free) in _retired)
            free();
        foreach (var image in _images.Values)
        {
            image.Staging?.Dispose();
            image.Image.Dispose();
        }
        foreach (var frame in _frames)
            frame.Buffer?.Dispose();

        _device.DestroyPipeline(_pipeline, null);
        _device.DestroyPipelineLayout(_pipelineLayout, null);
        _device.DestroyDescriptorPool(_descriptorPool, null);
        _device.DestroyDescriptorSetLayout(_descriptorSetLayout, null);
        _device.DestroySampler(_sampler, null);
        _atlasImage.Dispose();
    }
}
