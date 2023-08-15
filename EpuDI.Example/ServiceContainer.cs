using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using EpuDI;

namespace EpuDI.Example
{

    [Composition]
    partial class ServiceContainer : ServiceContainerBase
    {
        [Factories]
        private readonly CompositionRootFactories _factories;
        private readonly ServiceContainer? _parent;

        public ServiceContainer(CompositionRootFactories factories)
        {
            this._factories = factories ?? throw new ArgumentNullException(nameof(factories));
        }

        public ServiceContainer(ServiceContainer parent)
        {
            this._parent = parent;
            this._factories = parent._factories;
        }

        //public IService Service => _factories.Service(this);

        //public IAnotherService AnotherService => _factories.AnotherService(this);

        //public IAnotherService ThirdService => _factories.ThirdService(this);

        public ServiceContainer CreateScope()
        {
            return new ServiceContainer(this);
        }
    }
}
