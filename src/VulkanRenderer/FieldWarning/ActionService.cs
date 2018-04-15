using System;
using Tectonic;

namespace FieldWarning
{
    public class ActionService
        : GameService, IUpdatable
    {
        private readonly IServiceProvider provider;
        private readonly IUpdateLoopService updateLoop;
        private readonly Action<IServiceProvider> action;

        public ActionService(IServiceProvider provider, IUpdateLoopService updateLoop, Action<IServiceProvider> action)
        {
            this.provider = provider;
            this.updateLoop = updateLoop;
            this.action = action;
        }

        public override void Start()
        {
            this.updateLoop.Register(this, UpdateStage.Update);
        }

        public void Update()
        {
            this.action(this.provider);
        }
    }
}
