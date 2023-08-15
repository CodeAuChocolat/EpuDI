namespace EpuDI.Example
{
    partial class Program
    {
        static void Main(string[] args)
        {
            var factories = new CompositionRootFactories();
            var compositionRoot = new ServiceContainer(factories);
            Console.WriteLine(compositionRoot.Service.ToString());
            Console.WriteLine(compositionRoot.AnotherService.ToString());
            Console.WriteLine(compositionRoot.ScopedService.ToString());
            Console.WriteLine(compositionRoot.SingletonService.ToString());
            //Console.WriteLine("Root: " + compositionRoot.InstanceCountService.Count);
            //Console.WriteLine("Root: " + compositionRoot.InstanceCountService.Count);

            using(var scope = compositionRoot.CreateScope())
            {
                Console.WriteLine("Scope: " + scope.InstanceCountService.Count);
                Console.WriteLine("Scope: " + scope.InstanceCountService.Count);
                Console.WriteLine("Root: " + compositionRoot.InstanceCountService.Count);
            }
        }

        static partial void HelloFrom(string name);
    }
}