using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using GlmSharp;
using Microsoft.Extensions.Logging;
using SharpVk;
using Tectonic;

namespace FieldWarning
{
    public class PbrStage
        : RenderStage
    {
        private readonly ILogger<PbrStage> logger;

        private class Stub
            : IDisposable
        {
            public void Dispose()
            {
            }
        }

        private struct UBOMatrices
        {
            public mat4 Projection;
            public mat4 Model;
            public mat4 View;
            public vec3 CamPos;
            public float FlipUV;
        }

        private struct UBOParams
        {
            public UBOParams(float prefilteredCubeMipLevels = 0)
            {
                this.LightDir = new vec4(0.0f, -0.5f, -0.5f, 1.0f);
                this.Exposure = 4.5f;
                this.Gamma = 2.2f;
                this.PrefilteredCubeMipLevels = prefilteredCubeMipLevels;
            }

            public vec4 LightDir;
            public float Exposure;
            public float Gamma;
            public float PrefilteredCubeMipLevels;
        }

        private struct PushConstBlockMaterial
        {
            public float HasBaseColorTexture;
            public float HasMetallicRoughnessTexture;
            public float HasNormalTexture;
            public float HasOcclusionTexture;
            public float HasEmissiveTexture;
            public float MetallicFactor;
            public float RoughnessFactor;
            public float AlphaMask;
            public float AlphaMaskCutoff;
        }

        private class PbrState
            : IRenderStageState
        {
            public CombinedImageSampler BrdfLookup;
            public ShaderModule VertexShader;
            public ShaderModule FragmentShader;
            public PipelineLayout PipelineLayout;
            public DescriptorPool DescriptorPool;
            public DescriptorSetLayout[] DescriptorSetLayouts;
            public DescriptorSet[] DescriptorSets;
            public VulkanBuffer StateBuffer;
            public IMeshHandle SkyboxMesh;

            public IMeshHandle Mesh;

            public Device Device { get; set; }

            public void Dispose()
            {
                this.BrdfLookup?.Dispose();

                this.VertexShader?.Dispose();
                this.FragmentShader?.Dispose();

                this.StateBuffer?.Release();

                this.PipelineLayout?.Dispose();

                foreach (var layout in this.DescriptorSetLayouts)
                {
                    layout.Dispose();
                }

                this.DescriptorPool?.Dispose();

                this.SkyboxMesh?.Dispose();
            }
        }

        public class CombinedImageSampler
            : IDisposable
        {
            public CombinedImageSampler(VulkanImage image, ImageView view, Sampler sampler)
            {
                this.Image = image;
                this.View = view;
                this.Sampler = sampler;
            }

            public VulkanImage Image { get; private set; }

            public ImageView View { get; private set; }

            public Sampler Sampler { get; private set; }

            public void Dispose()
            {
                this.Image?.Dispose();
                this.View?.Dispose();
                this.Sampler?.Dispose();
            }
        }

        public IMeshHandle Mesh { get; set; }

        public PbrStage(ILogger<PbrStage> logger)
        {
            this.logger = logger;
        }

        public override IRenderStageState Initialise(Device device, VulkanBufferManager bufferManager, IHandleCreator handleCreator)
        {
            var brdfLookup = GenerateBrdfLookup(device, bufferManager);

            var vertexShader = LoadShaderModule(device, ".\\data\\shaders\\pbr.vert.spv");
            var fragmentShader = LoadShaderModule(device, ".\\data\\shaders\\pbr.frag.spv");

            var descriptorPool = device.CreateDescriptorPool(16, new[] { new DescriptorPoolSize(DescriptorType.UniformBuffer, 16), new DescriptorPoolSize(DescriptorType.CombinedImageSampler, 16) });

            var sceneDescriptorSetLayout = device.CreateDescriptorSetLayout(
                new[]
                {
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 0,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.UniformBuffer,
                        StageFlags = ShaderStageFlags.Vertex | ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 1,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.UniformBuffer,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 2,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 3,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 4,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    }
                });

            var materialDescriptorSetLayout = device.CreateDescriptorSetLayout(
                new[]
                {
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 0,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 1,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 2,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 3,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 4,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    }
                });

            var stateBuffer = bufferManager.CreateBuffer(512, BufferUsageFlags.UniformBuffer, MemoryPropertyFlags.DeviceLocal);

            var descriptorSets = descriptorPool.AllocateDescriptorSets(new[] { sceneDescriptorSetLayout, materialDescriptorSetLayout });

            descriptorSets[0].WriteDescriptorSet(0, 0, DescriptorType.UniformBuffer, new DescriptorBufferInfo
            {
                Buffer = stateBuffer.Buffer,
                Offset = 0,
                Range = Constants.WholeSize
            });

            descriptorSets[0].WriteDescriptorSet(1, 0, DescriptorType.UniformBuffer, new DescriptorBufferInfo
            {
                Buffer = stateBuffer.Buffer,
                Offset = 0,
                Range = Constants.WholeSize
            });

            var pipelineLayout = device.CreatePipelineLayout(
                new[] { sceneDescriptorSetLayout, materialDescriptorSetLayout },
                new PushConstantRange
                {
                    Offset = 0,
                    Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<PushConstBlockMaterial>(),
                    StageFlags = ShaderStageFlags.Fragment
                });

            return new PbrState
            {
                BrdfLookup = brdfLookup,
                Device = device,
                VertexShader = vertexShader,
                FragmentShader = fragmentShader,
                DescriptorPool = descriptorPool,
                DescriptorSetLayouts = new[] { sceneDescriptorSetLayout, materialDescriptorSetLayout },
                DescriptorSets = descriptorSets,
                StateBuffer = stateBuffer,
                PipelineLayout = pipelineLayout,
                Mesh = this.Mesh
            };
        }

        private CombinedImageSampler GenerateBrdfLookup(Device device, VulkanBufferManager bufferManager)
        {
            this.logger.LogDebug("Generating BRDF Lookup Table");

            long start = Stopwatch.GetTimestamp();

            const uint size = 512;
            const Format format = Format.R16G16SFloat;
            const string vertexShaderFilePath = ".\\data\\shaders\\genbrdflut.vert.spv";
            const string fragmentShaderFilePath = ".\\data\\shaders\\genbrdflut.frag.spv";

            var image = bufferManager.CreateImage(size, size, format, ImageTiling.Optimal, ImageUsageFlags.ColorAttachment | ImageUsageFlags.Sampled, MemoryPropertyFlags.DeviceLocal, false);
            var view = device.CreateImageView(image.Image, ImageViewType.ImageView2d, format, ComponentMapping.Identity, new ImageSubresourceRange(ImageAspectFlags.Color, 0, 1, 0, 1));
            var sampler = device.CreateSampler(Filter.Linear, Filter.Linear, SamplerMipmapMode.Linear, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, 0, false, 1, false, CompareOp.Always, 0, 1, BorderColor.FloatOpaqueWhite, false);
            var renderPass = device.CreateRenderPass(new AttachmentDescription(AttachmentDescriptionFlags.None, format, SampleCountFlags.SampleCount1, AttachmentLoadOp.Clear, AttachmentStoreOp.Store, AttachmentLoadOp.DontCare, AttachmentStoreOp.DontCare, ImageLayout.Undefined, ImageLayout.ShaderReadOnlyOptimal),
            new SubpassDescription
            {
                ColorAttachments = new[] { new AttachmentReference(0, ImageLayout.ColorAttachmentOptimal) }
            },
            new[]
            {
                new SubpassDependency(Constants.SubpassExternal, 0, PipelineStageFlags.BottomOfPipe, PipelineStageFlags.ColorAttachmentOutput, AccessFlags.MemoryRead, AccessFlags.ColorAttachmentRead | AccessFlags.ColorAttachmentWrite, DependencyFlags.ByRegion),
                new SubpassDependency(0, Constants.SubpassExternal, PipelineStageFlags.ColorAttachmentOutput, PipelineStageFlags.BottomOfPipe, AccessFlags.ColorAttachmentRead | AccessFlags.ColorAttachmentWrite, AccessFlags.MemoryRead, DependencyFlags.ByRegion)
            });

            var frameBuffer = device.CreateFramebuffer(renderPass, view, size, size, 1);

            var decriptorSetLayout = device.CreateDescriptorSetLayout(null);

            var pipelineLayout = device.CreatePipelineLayout(decriptorSetLayout, null);

            var vertexShader = LoadShaderModule(device, vertexShaderFilePath);
            var fragmentShader = LoadShaderModule(device, fragmentShaderFilePath);

            var pipeline = device.CreateGraphicsPipeline(null,
                new[]
                {
                    new PipelineShaderStageCreateInfo
                    {
                        Module = vertexShader,
                        Name = "main",
                        Stage = ShaderStageFlags.Vertex
                    },
                    new PipelineShaderStageCreateInfo
                    {
                        Module = fragmentShader,
                        Name = "main",
                        Stage = ShaderStageFlags.Fragment
                    }
                },
                new PipelineVertexInputStateCreateInfo(),
                new PipelineInputAssemblyStateCreateInfo { Topology = PrimitiveTopology.TriangleList },
                new PipelineRasterizationStateCreateInfo
                {
                    CullMode = CullModeFlags.None,
                    FrontFace = FrontFace.CounterClockwise,
                    PolygonMode = PolygonMode.Fill,
                    LineWidth = 1.0f
                },
                pipelineLayout,
                renderPass,
                0,
                null,
                -1,
                colorBlendState: new PipelineColorBlendStateCreateInfo
                {
                    Attachments = new[]
                    {
                        new PipelineColorBlendAttachmentState { ColorWriteMask = ColorComponentFlags.R | ColorComponentFlags.G | ColorComponentFlags.B | ColorComponentFlags.A }
                    }
                },
                depthStencilState: new PipelineDepthStencilStateCreateInfo { },
                viewportState: new PipelineViewportStateCreateInfo
                {
                    Viewports = new[] { new Viewport(0, 0, size, size, 0, 1) },
                    Scissors = new[] { new Rect2D(new Extent2D(size, size)) }
                },
                multisampleState: new PipelineMultisampleStateCreateInfo
                {
                    RasterizationSamples = SampleCountFlags.SampleCount1
                });

            vertexShader.Dispose();
            fragmentShader.Dispose();

            var tempPool = device.CreateCommandPool(0);
            var cmdBuffer = device.AllocateCommandBuffer(tempPool, CommandBufferLevel.Primary);

            cmdBuffer.Begin();
            cmdBuffer.BeginRenderPass(renderPass, frameBuffer, new Rect2D(new Extent2D(size, size)), (ClearValue)new ClearColorValue(0f, 0f, 0f, 1f), SubpassContents.Inline);
            cmdBuffer.BindPipeline(PipelineBindPoint.Graphics, pipeline);
            cmdBuffer.Draw(3, 1, 0, 0);
            cmdBuffer.EndRenderPass();
            cmdBuffer.End();

            var queue = device.GetQueue(0, 0);
            queue.Submit(new SubmitInfo { CommandBuffers = new[] { cmdBuffer } }, null);
            queue.WaitIdle();

            tempPool.Dispose();
            pipeline.Dispose();
            pipelineLayout.Dispose();
            decriptorSetLayout.Dispose();
            frameBuffer.Dispose();
            renderPass.Dispose();

            this.logger.LogDebug("BRDF Lookup Table took {duration} seconds", (float)((double)(Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency));

            return new CombinedImageSampler(image, view, sampler);
        }

        public override bool IsValid(IRenderStageState state)
        {
            return ((PbrState)state).Mesh == this.Mesh;
        }

        public override IDisposable Bind(IRenderStageState state, RenderPass renderPass, CommandBuffer commandBuffer, Extent2D targetExtent)
        {
            var stageState = (PbrState)state;

            var pipeline = stageState.Device.CreateGraphicsPipeline(null,
                                                                    new[]
                                                                    {
                                                                        new PipelineShaderStageCreateInfo
                                                                        {
                                                                            Stage = ShaderStageFlags.Vertex,
                                                                            Module = stageState.VertexShader,
                                                                            Name = "main"
                                                                        },
                                                                        new PipelineShaderStageCreateInfo
                                                                        {
                                                                            Stage = ShaderStageFlags.Fragment,
                                                                            Module = stageState.FragmentShader,
                                                                            Name = "main"
                                                                        }
                                                                    },
                                                                    new PipelineVertexInputStateCreateInfo
                                                                    {
                                                                        VertexBindingDescriptions = new[] { stageState.Mesh.GetBindingDescription(0) },
                                                                        VertexAttributeDescriptions = stageState.Mesh.GetAttributeDescriptions(0, 0)
                                                                    },
                                                                    new PipelineInputAssemblyStateCreateInfo { Topology = PrimitiveTopology.TriangleList },
                                                                    new PipelineRasterizationStateCreateInfo
                                                                    {
                                                                        PolygonMode = PolygonMode.Fill,
                                                                        LineWidth = 1,
                                                                        CullMode = CullModeFlags.Back,
                                                                        FrontFace = FrontFace.CounterClockwise
                                                                    },
                                                                    stageState.PipelineLayout,
                                                                    renderPass,
                                                                    0,
                                                                    null,
                                                                    -1,
                                                                    viewportState: new PipelineViewportStateCreateInfo
                                                                    {
                                                                        Viewports = new[] { new Viewport(0, 0, targetExtent.Width, targetExtent.Height, 0, 1) },
                                                                        Scissors = new[] { new Rect2D(targetExtent) }
                                                                    },
                                                                    multisampleState: new PipelineMultisampleStateCreateInfo
                                                                    {
                                                                        SampleShadingEnable = false,
                                                                        RasterizationSamples = SampleCountFlags.SampleCount1,
                                                                        MinSampleShading = 1
                                                                    },
                                                                    colorBlendState: new PipelineColorBlendStateCreateInfo
                                                                    {
                                                                        Attachments = new[]
                                                                        {
                                                                            new PipelineColorBlendAttachmentState
                                                                            {
                                                                                ColorWriteMask = ColorComponentFlags.R
                                                                                                    | ColorComponentFlags.G
                                                                                                    | ColorComponentFlags.B
                                                                                                    | ColorComponentFlags.A,
                                                                                BlendEnable = false,
                                                                                SourceColorBlendFactor = BlendFactor.One,
                                                                                DestinationColorBlendFactor = BlendFactor.Zero,
                                                                                ColorBlendOp = BlendOp.Add,
                                                                                SourceAlphaBlendFactor = BlendFactor.One,
                                                                                DestinationAlphaBlendFactor = BlendFactor.Zero,
                                                                                AlphaBlendOp = BlendOp.Add
                                                                            }
                                                                        },
                                                                        LogicOpEnable = false,
                                                                        LogicOp = LogicOp.Copy,
                                                                        BlendConstants = (0, 0, 0, 0)
                                                                    },
                                                                    depthStencilState: new PipelineDepthStencilStateCreateInfo
                                                                    {
                                                                        DepthTestEnable = true,
                                                                        DepthWriteEnable = true,
                                                                        DepthCompareOp = CompareOp.Less,
                                                                        DepthBoundsTestEnable = false,
                                                                        MinDepthBounds = 0,
                                                                        MaxDepthBounds = 1,
                                                                        StencilTestEnable = false
                                                                    });

            commandBuffer.BindDescriptorSets(PipelineBindPoint.Graphics, stageState.PipelineLayout, 0, stageState.DescriptorSets, null);

            commandBuffer.PushConstants(stageState.PipelineLayout, ShaderStageFlags.Fragment, 0, GetBytes(new PushConstBlockMaterial
            {
            }));

            commandBuffer.BindPipeline(PipelineBindPoint.Graphics, pipeline);

            stageState.Mesh.BindBuffers(commandBuffer, 0);

            //commandBuffer.DrawIndexed((uint)stageState.Mesh.IndexCount, 1, 0, 0, 0);

            return new DisposeGroup(pipeline);
        }

        private static ShaderModule LoadShaderModule(Device device, string filePath)
        {
            var code = LoadShaderData(filePath, out int codeSize);

            return device.CreateShaderModule(codeSize, code);
        }

        private static uint[] LoadShaderData(string filePath, out int codeSize)
        {
            var fileBytes = File.ReadAllBytes(filePath);
            var shaderData = new uint[(int)Math.Ceiling(fileBytes.Length / 4f)];

            System.Buffer.BlockCopy(fileBytes, 0, shaderData, 0, fileBytes.Length);

            codeSize = fileBytes.Length;

            return shaderData;
        }

        private static byte[] GetBytes<T>(T value)
        {
            int size = Marshal.SizeOf<T>();
            byte[] result = new byte[size];

            IntPtr dataPointer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(value, dataPointer, false);
            Marshal.Copy(dataPointer, result, 0, size);
            Marshal.FreeHGlobal(dataPointer);

            return result;
        }
    }
}
