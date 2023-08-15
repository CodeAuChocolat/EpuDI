using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EpuDI;

namespace EpuDI.Example
{
    [Factories]
    internal class CompositionRootFactories
    {
        [Transient]
        public IService Service(ServiceContainer services) => new Service();
        
        [Transient]
        public IAnotherService AnotherService(ServiceContainer services) => new AnotherService(services.Service);

        [Scoped]
        public IAnotherService ScopedService(ServiceContainer services) => new AnotherService(services.Service);

        [Singleton]
        public IAnotherService SingletonService(ServiceContainer services) => new AnotherService(services.Service);

        [Singleton]
        public IInstanceCountService InstanceCountService(ServiceContainer services) => new InstanceCountService();

        [Scoped]
        public IList<string> SomeStrings(ServiceContainer _) => new List<string>();

        [Scoped]
        public IList<string?> NullableStrings(ServiceContainer _) => new List<string?>();

        //public Func<ServiceContainer, IAnotherService> ThirdService = services => new AnotherService(services.Service);
    }
}
