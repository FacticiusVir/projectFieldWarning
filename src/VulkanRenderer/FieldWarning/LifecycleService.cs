using GlmSharp;
using SharpVk.Shanq;
using SharpVk.Shanq.GlmSharp;
using System.Linq;
using Tectonic;

namespace FieldWarning
{
    public class LifecycleService
        : GameService, IUpdatable
    {
        private readonly IUpdateLoopService updateLoop;
        private readonly IVulkanService vulkan;

        private Game game;
        private VulkanRenderMap renderMap;

        public LifecycleService(IUpdateLoopService updateLoop, IVulkanService vulkan)
        {
            this.updateLoop = updateLoop;
            this.vulkan = vulkan;
        }

        public override void Initialise(Game game)
        {
            this.game = game;
        }

        public override void Start()
        {
            this.updateLoop.Register(this, UpdateStage.Update);

            var meshStage = new MeshStage();

            this.renderMap = this.vulkan.CreateSimpleRenderMap((1440, 960),
                                                                "Project Field Warning",
                                                                new ClearStage { ClearColour = new vec4(0, 0.5f, 1, 1) },
                                                                new QuadStage(),
                                                                meshStage);
            
            meshStage.Mesh = this.renderMap.CreateStaticMesh<Vertex>(VectorTypeLibrary.Instance, this.vertices, this.indices);
        }

        public override void Stop()
        {
            this.updateLoop.Deregister(this);
        }

        public void Update()
        {
            if (this.renderMap.Endpoints.Any(x => x.ShouldClose))
            {
                this.renderMap.Close();

                this.renderMap = null;

                this.game.SignalStop();
            }
            else
            {
                this.renderMap.Render();
            }
        }

        private readonly Vertex[] vertices =
        {
            new Vertex(new vec3(0.5f, 0.5f, 0f)),
            new Vertex(new vec3(0.5f, -0.5f, 0f)),
            new Vertex(new vec3(-0.5f, -0.5f, 0f)),
            new Vertex(new vec3(-0.5f, 0.5f, 0f))
        };

        private readonly uint[] indices = { 0, 1, 2, 2, 3, 0 };

        private struct Vertex
        {
            public Vertex(vec3 position)
                : this(position, position)
            {
            }

            public Vertex(vec3 position, vec3 normal)
            {
                this.Position = position;
                this.Normal = normal;
            }

            [Location(0)]
            public vec3 Position;

            [Location(1)]
            public vec3 Normal;
        }
    }
}
